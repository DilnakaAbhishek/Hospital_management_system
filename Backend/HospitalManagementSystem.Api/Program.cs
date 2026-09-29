using Microsoft.EntityFrameworkCore;
using Npgsql;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Middleware;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using HospitalManagementSystem.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;
using HospitalManagementSystem.Api.AgenticAI.SafeTriage;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
var jwtSecret = builder.Configuration["Jwt:Secret"];
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var allowedOrigins = configuredOrigins
    .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be configured through a local secret or environment variable and contain at least 32 characters.");

if (!builder.Environment.IsEnvironment("Testing"))
{
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured through a local secret or environment variable.");
    if (!builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
        throw new InvalidOperationException("Cors:AllowedOrigins must contain the permitted web application origin(s).");
}
builder.Logging.ClearProviders();
builder.Logging.AddConsole();


// ---------- Services ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Hospital Management System API",
        Version = "v1",
        Description = "SE3090 Assignment 1 – Patient Management API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Enter the JWT received from the login endpoint."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "MediCore.Api",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "MediCore.Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
builder.Services.AddAuthorization();

// PostgreSQL + EF Core
if (!string.IsNullOrWhiteSpace(connectionString))
{
    var databaseConnection = new NpgsqlConnectionStringBuilder(connectionString);
    // Allow remote database connections more time to establish (handles Neon serverless cold-starts)
    if (!databaseConnection.ContainsKey("Timeout") || databaseConnection.Timeout < 45)
        databaseConnection.Timeout = 45;

    // Send TCP keepalive probes every 15 seconds to prevent cloud proxies / Neon from silently dropping idle connections
    if (!databaseConnection.ContainsKey("KeepAlive"))
        databaseConnection.KeepAlive = 15;

    // Refresh idle connections before cloud idle-disconnects
    if (!databaseConnection.ContainsKey("Connection Idle Lifetime"))
        databaseConnection.ConnectionIdleLifetime = 60;

    connectionString = databaseConnection.ConnectionString;
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(60);
    }));

// Dependency Injection
builder.Services.AddSmsNotifications(builder.Configuration);
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddHostedService<AppointmentCompletionService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IDoctorScheduleService, DoctorScheduleService>();
builder.Services.AddScoped<ITriageWorkflowService>(provider => new TriageWorkflowService(
    provider.GetRequiredService<ApplicationDbContext>(),
    provider.GetRequiredService<ILogger<TriageWorkflowService>>(),
    provider.GetRequiredService<ISafeTriageSemanticExtractionAgent>(),
    provider.GetRequiredService<ISafeTriageQuestionPlanningAgent>(),
    provider.GetRequiredService<ISafeTriageResponseGenerationAgent>(),
    builder.Configuration.GetSection("SafeTriage").Get<SafeTriageOptions>() ?? new SafeTriageOptions()));
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.AssistantAgentRegistry>();
builder.Services.AddScoped<HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.HospitalAssistantService>();
builder.Services.AddHttpClient<HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.IAssessmentIntentClient,
    HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.GeminiAssessmentIntentClient>((provider, client) =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
});
// Reusable controlled tools for the Member 3 proposal and Member 4 confirmation agents.
builder.Services.AddScoped<IAppointmentAgentTools, AppointmentAgentTools>();
builder.Services.AddScoped<IAppointmentSearchTools>(sp => sp.GetRequiredService<IAppointmentAgentTools>());
builder.Services.AddScoped<IHospitalAppointmentProposalAgent>(sp => new HospitalAppointmentProposalAgent(
    sp.GetRequiredService<IAppointmentSearchTools>(),
    sp.GetRequiredService<IAppointmentProposalStore>(),
    sp.GetService<IGeminiAppointmentIntelligenceClient>()));
builder.Services.AddHttpClient<IGeminiAppointmentIntelligenceClient, GeminiAppointmentIntelligenceClient>((provider, client) =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    var key = provider.GetRequiredService<IConfiguration>()["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-goog-api-key", key);
});
builder.Services.AddScoped<IAppointmentProposalStore, AppointmentProposalStore>();
builder.Services.AddScoped<ISafetyApprovalTools, SafetyApprovalTools>();
builder.Services.AddScoped<ISafetyValidationApprovalAgent, SafetyValidationApprovalAgent>();
builder.Services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
builder.Services.AddScoped<IFileStorageService, CloudflareR2StorageService>();
builder.Services.AddScoped<IPlanningCoordinatorStore, PlanningCoordinatorStore>();
builder.Services.AddScoped<IPlanningCoordinatorAgent>(sp => new PlanningCoordinatorAgent(sp.GetRequiredService<IPlanningModelClient>(), sp.GetRequiredService<IPlanningCoordinatorStore>(), sp.GetRequiredService<ILogger<PlanningCoordinatorAgent>>()));
builder.Services.AddHttpClient<IPlanningModelClient, GeminiPlanningModelClient>((provider, client) =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    var key = provider.GetRequiredService<IConfiguration>()["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-goog-api-key", key);
});
// SafeTriage owns separate Gemini stages so the shared PatientCare extractor remains behaviorally unchanged.
builder.Services.AddHttpClient<ISafeTriageSemanticExtractionAgent, GeminiSafeTriageSemanticExtractionAgent>((provider, client) =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    var key = provider.GetRequiredService<IConfiguration>()["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-goog-api-key", key);
});
builder.Services.AddHttpClient<ISafeTriageQuestionPlanningAgent, GeminiSafeTriageQuestionPlanningAgent>((provider, client) =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    var key = provider.GetRequiredService<IConfiguration>()["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-goog-api-key", key);
});
builder.Services.AddHttpClient<ISafeTriageResponseGenerationAgent, GeminiSafeTriageResponseGenerationAgent>((provider, client) =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    var key = provider.GetRequiredService<IConfiguration>()["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-goog-api-key", key);
});
builder.Services.AddHttpClient<HospitalManagementSystem.Api.AgenticAI.MedicalReports.IMedicalRecordIntelligenceAgent, HospitalManagementSystem.Api.AgenticAI.MedicalReports.GeminiMedicalRecordClient>((provider, client) =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    var key = provider.GetRequiredService<IConfiguration>()["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-goog-api-key", key);
});
builder.Services.AddScoped<HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.IHospitalAssistantReadAgent, HospitalManagementSystem.Api.AgenticAI.MedicalReports.MedicalReportAssistantAgent>();


// In Development mode, dynamically allow any localhost origin (supporting changing Flutter Web ports).
// In Production mode, strictly enforce configured AllowedOrigins.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && (uri.Host == "localhost" || uri.Host == "127.0.0.1"))
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    });
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (app.Environment.IsEnvironment("Testing"))
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();

    // Seed accounts use documented sample passwords and must never be created in a deployed environment.
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        await SeedSampleDataAsync(db);
}

// ---------- Middleware ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseCors("AppCors");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow
}));

app.Run();

static async Task SeedSampleDataAsync(ApplicationDbContext db)
{
    const string adminEmail = "admin@medicore.lk";
    if (!await db.Users.AnyAsync(user => user.Email == adminEmail))
    {
        var passwordHasher = new PasswordHasher<User>();
        var admin = new User { FullName = "System Administrator", Email = adminEmail, Role = "Admin" };
        admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin1234");
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }

    if (!await db.Users.AnyAsync(user => user.Email == "amal.perera@email.com"))
    {
        var passwordHasher = new PasswordHasher<User>();
        var user1 = new User { FullName = "Amal Perera", Email = "amal.perera@email.com", Role = "Patient" };
        user1.PasswordHash = passwordHasher.HashPassword(user1, "Patient123!");
        db.Users.Add(user1);
        await db.SaveChangesAsync();
    }

    if (!await db.Users.AnyAsync(user => user.Email == "nimesha.silva@email.com"))
    {
        var passwordHasher = new PasswordHasher<User>();
        var user = new User { FullName = "Nimesha Silva", Email = "nimesha.silva@email.com", Role = "Patient" };
        user.PasswordHash = passwordHasher.HashPassword(user, "Patient123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    const string dilnakaEmail = "dilnaka.perera@medicore.lk";
    if (!await db.Users.AnyAsync(u => u.Email == dilnakaEmail))
    {
        var passwordHasher = new PasswordHasher<User>();
        var user = new User { FullName = "Dr. Dilnaka Perera", Email = dilnakaEmail, Role = "Doctor" };
        user.PasswordHash = passwordHasher.HashPassword(user, "Doctor123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
        db.Doctors.Add(new HospitalManagementSystem.Api.Models.Doctor
        {
            UserId = user.UserId,
            FirstName = "Dr. Dilnaka",
            LastName = "Perera",
            NIC = "198923456781",
            Specialization = "General Medicine",
            SlmcLicenseNumber = "SLMC-7812",
            PhoneNumber = "0771234567",
            RegistrationStatus = HospitalManagementSystem.Api.Models.DoctorRegistrationStatuses.Approved,
            ReviewedByUserId = admin?.UserId,
            ReviewedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    const string kasunEmail = "kasun.silva@medicore.lk";
    if (!await db.Users.AnyAsync(u => u.Email == kasunEmail))
    {
        var passwordHasher = new PasswordHasher<User>();
        var user = new User { FullName = "Dr. Kasun Silva", Email = kasunEmail, Role = "Doctor" };
        user.PasswordHash = passwordHasher.HashPassword(user, "Doctor123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
        db.Doctors.Add(new HospitalManagementSystem.Api.Models.Doctor
        {
            UserId = user.UserId,
            FirstName = "Dr. Kasun",
            LastName = "Silva",
            NIC = "199134567892",
            Specialization = "Cardiologist",
            SlmcLicenseNumber = "SLMC-9023",
            PhoneNumber = "0719876543",
            RegistrationStatus = HospitalManagementSystem.Api.Models.DoctorRegistrationStatuses.Approved,
            ReviewedByUserId = admin?.UserId,
            ReviewedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    if (!await db.Patients.AnyAsync())
    {
        db.Patients.AddRange(
            new HospitalManagementSystem.Api.Models.Patient
            {
                FirstName = "Amal",
                LastName = "Perera",
                DateOfBirth = new DateTime(1985, 3, 14, 0, 0, 0, DateTimeKind.Utc),
                Gender = "Male",
                NIC = "850314123V",
                PhoneNumber = "+94 77 234 5678",
                Email = "amal.perera@email.com",
                Address = "45 Galle Rd, Colombo 03",
                BloodGroup = "B+",
                EmergencyContactName = "Kamala Perera",
                EmergencyContactPhone = "+94 71 234 5678",
                CreatedAt = new DateTime(2024, 1, 10, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 10, 8, 0, 0, DateTimeKind.Utc)
            },
            new HospitalManagementSystem.Api.Models.Patient
            {
                FirstName = "Nimesha",
                LastName = "Silva",
                DateOfBirth = new DateTime(1992, 7, 22, 0, 0, 0, DateTimeKind.Utc),
                Gender = "Female",
                NIC = "920722234V",
                PhoneNumber = "+94 76 345 6789",
                Email = "nimesha.silva@email.com",
                Address = "12 Kandy Rd, Peradeniya",
                BloodGroup = "O+",
                EmergencyContactName = "Ruwan Silva",
                EmergencyContactPhone = "+94 70 345 6789",
                CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            }
        );

        await db.SaveChangesAsync();
    }

    if (!await db.DoctorTimeSlots.AnyAsync())
    {
        db.DoctorTimeSlots.AddRange(
            new HospitalManagementSystem.Api.Models.DoctorTimeSlot
            {
                DoctorName = "Dr. Priyantha Jayawardena",
                Specialty = "General Medicine",
                StartAt = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc),
                EndAt = new DateTime(2026, 8, 8, 11, 0, 0, DateTimeKind.Utc),
                Capacity = 4,
                IsActive = true,
                CreatedAt = new DateTime(2026, 8, 8, 4, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 4, 0, 0, DateTimeKind.Utc)
            },
            new HospitalManagementSystem.Api.Models.DoctorTimeSlot
            {
                DoctorName = "Dr. Chamari Gunaratne",
                Specialty = "Cardiology",
                StartAt = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc),
                EndAt = new DateTime(2026, 8, 8, 16, 0, 0, DateTimeKind.Utc),
                Capacity = 3,
                IsActive = true,
                CreatedAt = new DateTime(2026, 8, 8, 4, 15, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 4, 15, 0, DateTimeKind.Utc)
            }
        );

        await db.SaveChangesAsync();
    }

    if (!await db.Appointments.AnyAsync())
    {
        var amalPatient = await db.Patients.SingleOrDefaultAsync(patient =>
            patient.Email == "amal.perera@email.com");
        var nimeshaPatient = await db.Patients.SingleOrDefaultAsync(patient =>
            patient.Email == "nimesha.silva@email.com");
        var generalMedicineSlot = await db.DoctorTimeSlots.SingleOrDefaultAsync(slot =>
            slot.DoctorName == "Dr. Priyantha Jayawardena" &&
            slot.StartAt == new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc));
        var cardiologySlot = await db.DoctorTimeSlots.SingleOrDefaultAsync(slot =>
            slot.DoctorName == "Dr. Chamari Gunaratne" &&
            slot.StartAt == new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc));

        if (amalPatient is null || nimeshaPatient is null ||
            generalMedicineSlot is null || cardiologySlot is null)
        {
            throw new InvalidOperationException(
                "Cannot seed sample appointments because their patients or doctor time slots are missing.");
        }

        db.Appointments.AddRange(
            new HospitalManagementSystem.Api.Models.Appointment
            {
                DoctorTimeSlotId = generalMedicineSlot.DoctorTimeSlotId,
                PatientId = amalPatient.PatientId,
                AppointmentNumber = 1,
                PatientName = "Amal Perera",
                PatientPhone = "+94 77 234 5678",
                PatientEmail = "amal.perera@email.com",
                AppointmentType = "Consultation",
                Reason = "Fever and cough",
                Status = "Confirmed",
                CreatedAt = new DateTime(2026, 8, 8, 4, 30, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 4, 30, 0, DateTimeKind.Utc)
            },
            new HospitalManagementSystem.Api.Models.Appointment
            {
                DoctorTimeSlotId = cardiologySlot.DoctorTimeSlotId,
                PatientId = nimeshaPatient.PatientId,
                AppointmentNumber = 1,
                PatientName = "Nimesha Silva",
                PatientPhone = "+94 76 345 6789",
                PatientEmail = "nimesha.silva@email.com",
                AppointmentType = "Follow-up",
                Reason = "Review ECG results",
                Status = "Confirmed",
                CreatedAt = new DateTime(2026, 8, 8, 5, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 5, 0, 0, DateTimeKind.Utc)
            }
        );
        await db.SaveChangesAsync();
    }

    if (!await db.MedicalRecords.AnyAsync())
    {
        var amal = await db.Patients.FirstOrDefaultAsync(p => p.Email == "amal.perera@email.com");
        var nimesha = await db.Patients.FirstOrDefaultAsync(p => p.Email == "nimesha.silva@email.com");
        var doctor = await db.Doctors.FirstOrDefaultAsync();

        if (amal != null)
        {
            db.MedicalRecords.Add(new HospitalManagementSystem.Api.Models.MedicalRecord
            {
                PatientId = amal.PatientId,
                DoctorId = doctor?.DoctorId,
                RecordDate = DateTime.UtcNow.AddDays(-5),
                RecordType = HospitalManagementSystem.Api.Models.MedicalRecordTypes.Consultation,
                Diagnosis = "Acute Upper Respiratory Tract Infection",
                Symptoms = "Low-grade fever, dry cough, mild sore throat for 3 days.",
                TreatmentPlan = "Rest, adequate hydration, Paracetamol 500mg TDS for 3 days.",
                PrescriptionNotes = "Paracetamol 500mg - 1 tab tid x 3 days\nCetirizine 10mg - 1 tab nocte x 5 days",
                LabNotes = "Chest clear on auscultation. Normal SpO2 (98%).",
                FollowUpDate = DateTime.UtcNow.AddDays(7),
                Status = HospitalManagementSystem.Api.Models.MedicalRecordStatuses.Finalized,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            });
        }

        if (nimesha != null)
        {
            var rec = new HospitalManagementSystem.Api.Models.MedicalRecord
            {
                PatientId = nimesha.PatientId,
                DoctorId = doctor?.DoctorId,
                RecordDate = DateTime.UtcNow.AddDays(-2),
                RecordType = HospitalManagementSystem.Api.Models.MedicalRecordTypes.LabReport,
                Diagnosis = "Mild Sinus Tachycardia",
                Symptoms = "Palpitations during moderate exercise, mild shortness of breath.",
                TreatmentPlan = "Cardiology review, 12-lead ECG, lifestyle management.",
                PrescriptionNotes = "Propranolol 10mg PRN",
                LabNotes = "ECG reveals normal axis, sinus tachycardia (HR 102 bpm). Normal troponin I.",
                FollowUpDate = DateTime.UtcNow.AddDays(14),
                Status = HospitalManagementSystem.Api.Models.MedicalRecordStatuses.Finalized,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            };
            rec.Attachments.Add(new HospitalManagementSystem.Api.Models.MedicalRecordAttachment
            {
                FileName = "ecg_report_20260818.pdf",
                FileType = "application/pdf",
                FileUrl = "/uploads/medical-records/ecg_report_20260818.pdf",
                FileSize = 1048576,
                UploadedAt = DateTime.UtcNow.AddDays(-2)
            });
            db.MedicalRecords.Add(rec);
        }

        await db.SaveChangesAsync();
    }
}

public partial class Program { }
