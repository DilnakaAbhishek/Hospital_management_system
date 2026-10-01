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
var allowAnyOrigin = configuredOrigins.Any(o => o?.Trim() == "*");
var allowedOrigins = configuredOrigins
    .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be configured through a local secret or environment variable and contain at least 32 characters.");

if (!builder.Environment.IsEnvironment("Testing") && !builder.Environment.IsEnvironment("Test"))
{
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured through a local secret or environment variable.");
    if (!builder.Environment.IsDevelopment() && allowedOrigins.Length == 0 && !allowAnyOrigin)
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
        if (allowAnyOrigin)
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else if (builder.Environment.IsDevelopment())
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

    if (app.Environment.IsEnvironment("Testing") || app.Environment.IsEnvironment("Test"))
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();

    // Seed accounts use documented sample passwords and must never be created in a deployed environment.
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing") || app.Environment.IsEnvironment("Test"))
        await SeedSampleDataAsync(db);
}

// ---------- Middleware ----------
// Enable Swagger in all environments so developers, evaluators, and testers can test the deployed API
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hospital Management System API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "MediCore API - Documentation";
});

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseCors("AppCors");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "MediCore Hospital Management System API",
    environment = app.Environment.EnvironmentName,
    message = "API backend is fully operational and healthy.",
    timestamp = DateTime.UtcNow
}));

// Root endpoint: Deployment verification & status page
app.MapGet("/", (HttpContext context) =>
{
    var accept = context.Request.Headers.Accept.ToString();
    if (accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Content(GetDeploymentSuccessHtml(app.Environment.EnvironmentName), "text/html; charset=utf-8");
    }

    return Results.Ok(new
    {
        status = "Success",
        message = "MediCore Hospital Management System API is deployed and running successfully!",
        service = "Hospital Management System API (MediCore)",
        version = "v1.0",
        environment = app.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow,
        documentation = "/swagger",
        health = "/health",
        endpoints = new
        {
            health = "/health",
            swagger = "/swagger",
            auth = "/api/auth/login",
            patients = "/api/patient",
            doctors = "/api/doctor",
            appointments = "/api/appointment",
            medicalRecords = "/api/medical-records"
        }
    });
});

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

static string GetDeploymentSuccessHtml(string environment)
{
    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>MediCore API - Deployed Successfully</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;600&display=swap" rel="stylesheet">
    <style>
        :root {
            --bg: #090d16;
            --card-bg: rgba(15, 23, 42, 0.78);
            --card-border: rgba(255, 255, 255, 0.08);
            --primary: #0284c7;
            --primary-light: #38bdf8;
            --emerald: #10b981;
            --text-main: #f8fafc;
            --text-muted: #94a3b8;
            --text-dim: #64748b;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: 'Plus Jakarta Sans', system-ui, -apple-system, sans-serif;
            background-color: var(--bg);
            background-image: 
                radial-gradient(circle at 10% 20%, rgba(2, 132, 199, 0.16) 0%, transparent 40%),
                radial-gradient(circle at 90% 80%, rgba(16, 185, 129, 0.14) 0%, transparent 40%),
                linear-gradient(180deg, #090d16 0%, #030712 100%);
            color: var(--text-main);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 24px;
        }
        .container {
            max-width: 680px;
            width: 100%;
        }
        .card {
            background: var(--card-bg);
            border: 1px solid var(--card-border);
            backdrop-filter: blur(20px);
            -webkit-backdrop-filter: blur(20px);
            border-radius: 24px;
            padding: 36px 36px 32px 36px;
            box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255, 255, 255, 0.04);
        }
        .top-bar {
            display: flex;
            align-items: center;
            justify-content: space-between;
            margin-bottom: 24px;
            flex-wrap: wrap;
            gap: 12px;
        }
        .brand {
            display: flex;
            align-items: center;
            gap: 12px;
        }
        .logo-box {
            width: 44px;
            height: 44px;
            border-radius: 12px;
            background: linear-gradient(135deg, #0284c7 0%, #0369a1 100%);
            display: flex;
            align-items: center;
            justify-content: center;
            box-shadow: 0 4px 14px rgba(2, 132, 199, 0.35);
        }
        .logo-box svg {
            width: 24px;
            height: 24px;
            fill: #ffffff;
        }
        .brand-text {
            font-size: 16px;
            font-weight: 700;
            letter-spacing: -0.01em;
            color: #ffffff;
        }
        .badge-live {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 6px 14px;
            border-radius: 9999px;
            background: rgba(16, 185, 129, 0.12);
            border: 1px solid rgba(16, 185, 129, 0.3);
            color: #34d399;
            font-size: 13px;
            font-weight: 600;
        }
        .pulse-dot {
            width: 8px;
            height: 8px;
            border-radius: 50%;
            background-color: var(--emerald);
            box-shadow: 0 0 10px var(--emerald);
            animation: pulse-ring 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
        }
        @keyframes pulse-ring {
            0%, 100% { opacity: 1; transform: scale(1); }
            50% { opacity: 0.4; transform: scale(0.8); }
        }
        h1 {
            font-size: 26px;
            font-weight: 800;
            letter-spacing: -0.02em;
            margin-bottom: 8px;
            background: linear-gradient(135deg, #ffffff 40%, #cbd5e1 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        .subtitle {
            color: var(--text-muted);
            font-size: 14.5px;
            line-height: 1.6;
            margin-bottom: 24px;
        }
        .alert-box {
            display: flex;
            align-items: center;
            gap: 12px;
            background: rgba(16, 185, 129, 0.08);
            border: 1px solid rgba(16, 185, 129, 0.22);
            border-radius: 14px;
            padding: 14px 18px;
            margin-bottom: 24px;
            color: #a7f3d0;
            font-size: 14px;
            font-weight: 500;
        }
        .grid {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 12px;
            margin-bottom: 26px;
        }
        @media (max-width: 480px) {
            .grid { grid-template-columns: 1fr; }
        }
        .metric-card {
            background: rgba(255, 255, 255, 0.025);
            border: 1px solid rgba(255, 255, 255, 0.06);
            border-radius: 14px;
            padding: 14px 16px;
        }
        .metric-card .label {
            font-size: 11px;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            color: var(--text-dim);
            margin-bottom: 4px;
        }
        .metric-card .val {
            font-size: 14px;
            font-weight: 600;
            color: #f1f5f9;
        }
        .btn-group {
            display: flex;
            gap: 12px;
            flex-wrap: wrap;
            margin-bottom: 28px;
        }
        .btn {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 11px 20px;
            border-radius: 12px;
            font-size: 14px;
            font-weight: 600;
            text-decoration: none;
            transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
        }
        .btn-primary {
            background: linear-gradient(135deg, #0284c7 0%, #0369a1 100%);
            color: #ffffff;
            box-shadow: 0 4px 14px rgba(2, 132, 199, 0.3);
        }
        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(2, 132, 199, 0.45);
        }
        .btn-secondary {
            background: rgba(255, 255, 255, 0.05);
            color: #e2e8f0;
            border: 1px solid rgba(255, 255, 255, 0.1);
        }
        .btn-secondary:hover {
            background: rgba(255, 255, 255, 0.09);
            color: #ffffff;
            transform: translateY(-2px);
        }
        .footer {
            border-top: 1px solid rgba(255, 255, 255, 0.06);
            padding-top: 18px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            font-size: 12px;
            color: var(--text-dim);
            flex-wrap: wrap;
            gap: 8px;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="card">
            <div class="top-bar">
                <div class="brand">
                    <div class="logo-box">
                        <svg viewBox="0 0 24 24">
                            <path d="M19 10.5h-5.5V5c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v5.5H5c-.83 0-1.5.67-1.5 1.5s.67 1.5 1.5 1.5h5.5V19c0 .83.67 1.5 1.5 1.5s1.5-.67 1.5-1.5v-5.5H19c.83 0 1.5-.67 1.5-1.5s-.67-1.5-1.5-1.5z"/>
                        </svg>
                    </div>
                    <div>
                        <div class="brand-text">MediCore API</div>
                    </div>
                </div>
                <div class="badge-live">
                    <span class="pulse-dot"></span>
                    <span>ONLINE & DEPLOYED</span>
                </div>
            </div>

            <h1>Backend Service Deployed Successfully</h1>
            <p class="subtitle">The MediCore Hospital Management System API backend is active, healthy, and successfully accepting incoming requests.</p>

            <div class="alert-box">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                    <polyline points="22 4 12 14.01 9 11.01"></polyline>
                </svg>
                <span>Deployment confirmed: All core services & controllers are loaded.</span>
            </div>

            <div class="grid">
                <div class="metric-card">
                    <div class="label">Environment</div>
                    <div class="val">{{environment}}</div>
                </div>
                <div class="metric-card">
                    <div class="label">Runtime Platform</div>
                    <div class="val">ASP.NET Core 8 Web API</div>
                </div>
                <div class="metric-card">
                    <div class="label">Database</div>
                    <div class="val">Neon PostgreSQL Serverless</div>
                </div>
                <div class="metric-card">
                    <div class="label">Agentic AI Engine</div>
                    <div class="val">SafeTriage & PlanningCoordinator</div>
                </div>
            </div>

            <div class="btn-group">
                <a href="/swagger" class="btn btn-primary">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"></path>
                        <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"></path>
                    </svg>
                    Swagger API Documentation
                </a>
                <a href="/health" class="btn btn-secondary">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"></polyline>
                    </svg>
                    Check Health Status
                </a>
            </div>

            <div class="footer">
                <span>SE3090 — Software Engineering Frameworks</span>
                <span>MediCore Hospital Management System</span>
            </div>
        </div>
    </div>
</body>
</html>
""";
}

public partial class Program { }
