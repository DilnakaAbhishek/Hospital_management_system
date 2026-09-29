using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.AgenticExecution> AgenticExecutions => Set<HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.AgenticExecution>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<DoctorTimeSlot> DoctorTimeSlots => Set<DoctorTimeSlot>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<AppointmentProposal> AppointmentProposals => Set<AppointmentProposal>();
        public DbSet<HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.AssistantConversation> AssistantConversations => Set<HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.AssistantConversation>();
        public DbSet<PatientCareAssessment> PatientCareAssessments => Set<PatientCareAssessment>();
        public DbSet<AppointmentNotification> AppointmentNotifications => Set<AppointmentNotification>();
        public DbSet<Doctor> Doctors => Set<Doctor>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<TriageWorkflow> TriageWorkflows => Set<TriageWorkflow>();
        public DbSet<TriageWorkflowEvent> TriageWorkflowEvents => Set<TriageWorkflowEvent>();
        public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
        public DbSet<MedicalRecordAttachment> MedicalRecordAttachments => Set<MedicalRecordAttachment>();
        public DbSet<MedicalRecordNotification> MedicalRecordNotifications => Set<MedicalRecordNotification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.AgenticExecution>(entity =>
            {
                entity.HasKey(x => x.WorkflowId);
                entity.HasIndex(x => x.PatientId);
                entity.Property(x => x.RecordJson).HasColumnType("jsonb");
            });
            modelBuilder.Entity<HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.AssistantConversation>(entity =>
            {
                entity.HasKey(x => x.AssistantConversationId);
                entity.Property(x => x.Title).HasMaxLength(120);
                entity.HasIndex(x => new { x.PatientId, x.InitialRequestId }).IsUnique();
                entity.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MedicalRecord>(entity =>
            {
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_MedicalRecords_Status",
                        "\"Status\" IN ('Draft', 'Finalized')");
                    t.HasCheckConstraint(
                        "CK_MedicalRecords_RecordType",
                        "\"RecordType\" IN ('Consultation', 'LabReport', 'DischargeSummary', 'Prescription', 'GeneralNote')");
                });
                entity.HasKey(m => m.MedicalRecordId);
                entity.Property(m => m.RecordType).IsRequired().HasMaxLength(50);
                entity.Property(m => m.Diagnosis).IsRequired().HasMaxLength(500);
                entity.Property(m => m.Symptoms).IsRequired().HasMaxLength(2000);
                entity.Property(m => m.TreatmentPlan).IsRequired().HasMaxLength(2000);
                entity.Property(m => m.PrescriptionNotes).HasMaxLength(2000);
                entity.Property(m => m.LabNotes).HasMaxLength(4000);
                entity.Property(m => m.Status).IsRequired().HasMaxLength(30);

                entity.HasIndex(m => new { m.PatientId, m.RecordDate });
                entity.HasIndex(m => m.DoctorId);
                entity.HasIndex(m => m.AppointmentId);
                entity.HasIndex(m => m.RecordType);
                entity.HasIndex(m => m.Status);

                entity.HasOne(m => m.Patient)
                    .WithMany()
                    .HasForeignKey(m => m.PatientId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Doctor)
                    .WithMany()
                    .HasForeignKey(m => m.DoctorId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(m => m.Appointment)
                    .WithMany()
                    .HasForeignKey(m => m.AppointmentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<MedicalRecordAttachment>(entity =>
            {
                entity.HasKey(a => a.AttachmentId);
                entity.Property(a => a.FileName).IsRequired().HasMaxLength(255);
                entity.Property(a => a.FileType).IsRequired().HasMaxLength(100);
                entity.Property(a => a.FileUrl).IsRequired().HasMaxLength(1000);
                entity.Property(a => a.FileSize).IsRequired();

                entity.HasIndex(a => a.MedicalRecordId);

                entity.HasOne(a => a.MedicalRecord)
                    .WithMany(m => m.Attachments)
                    .HasForeignKey(a => a.MedicalRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.FullName).IsRequired().HasMaxLength(150);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(200);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).IsRequired().HasMaxLength(50);
            });

            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Doctors_RegistrationStatus",
                    "\"RegistrationStatus\" IN ('Pending', 'Approved', 'Declined', 'DeletionPending')"));
                entity.HasKey(d => d.DoctorId);
                entity.Property(d => d.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(d => d.LastName).IsRequired().HasMaxLength(100);
                entity.Property(d => d.NIC).IsRequired().HasMaxLength(12);
                entity.Property(d => d.Specialization).IsRequired().HasMaxLength(100);
                entity.Property(d => d.SlmcLicenseNumber).IsRequired().HasMaxLength(50);
                entity.Property(d => d.PhoneNumber).IsRequired().HasMaxLength(15);
                entity.Property(d => d.RegistrationStatus).IsRequired().HasMaxLength(20);
                entity.Property(d => d.DeclineReason).HasMaxLength(500);
                entity.HasIndex(d => d.UserId).IsUnique();
                entity.HasIndex(d => d.NIC).IsUnique();
                entity.HasIndex(d => d.SlmcLicenseNumber).IsUnique();
                entity.HasIndex(d => d.RegistrationStatus);
                entity.HasOne(d => d.User)
                    .WithOne(u => u.DoctorProfile)
                    .HasForeignKey<Doctor>(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.ReviewedByUser)
                    .WithMany()
                    .HasForeignKey(d => d.ReviewedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Room>(entity =>
            {
                entity.HasKey(r => r.RoomId);
                entity.Property(r => r.RoomNumber).IsRequired().HasMaxLength(30);
                entity.Property(r => r.RoomName).IsRequired().HasMaxLength(100);
                entity.Property(r => r.Floor).IsRequired().HasMaxLength(50);
                entity.Property(r => r.Description).HasMaxLength(500);
                entity.HasIndex(r => r.RoomNumber).IsUnique();
                entity.HasIndex(r => r.IsConfirmed);
                entity.HasOne(r => r.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(r => r.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasKey(p => p.PatientId);
                entity.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(p => p.LastName).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Email).IsRequired().HasMaxLength(200);
                entity.HasIndex(p => p.Email).IsUnique();
                entity.Property(p => p.NIC).IsRequired().HasMaxLength(20);
                entity.HasIndex(p => p.NIC).IsUnique();
                entity.Property(p => p.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(p => p.Gender).IsRequired().HasMaxLength(20);
                entity.Property(p => p.BloodGroup).HasMaxLength(10);
                entity.Property(p => p.Address).HasMaxLength(500);
                entity.Property(p => p.EmergencyContactName).IsRequired(false).HasMaxLength(100);
                entity.Property(p => p.EmergencyContactPhone).IsRequired(false).HasMaxLength(20);
                entity.Property(p => p.ProfileImageUrl).HasMaxLength(500);
            });

            modelBuilder.Entity<DoctorTimeSlot>(entity =>
            {
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_DoctorTimeSlots_Capacity_Positive", "\"Capacity\" > 0");
                    t.HasCheckConstraint("CK_DoctorTimeSlots_TimeRange", "\"EndAt\" > \"StartAt\"");
                    t.HasCheckConstraint("CK_DoctorTimeSlots_ConsultationFee_NonNegative", "\"ConsultationFee\" >= 0");
                });
                entity.HasKey(s => s.DoctorTimeSlotId);
                entity.Property(s => s.DoctorName).IsRequired().HasMaxLength(150);
                entity.Property(s => s.Specialty).IsRequired().HasMaxLength(100);
                entity.Property(s => s.Capacity).IsRequired();
                entity.Property(s => s.ConsultationFee).HasPrecision(10, 2).IsRequired();
                entity.Property(s => s.IsActive).IsRequired();
                entity.HasIndex(s => new { s.DoctorName, s.StartAt, s.EndAt });
                entity.HasIndex(s => new { s.RoomId, s.StartAt, s.EndAt });
                entity.HasIndex(s => new { s.DoctorId, s.StartAt, s.EndAt });
                entity.HasOne(s => s.Room)
                    .WithMany(r => r.DoctorTimeSlots)
                    .HasForeignKey(s => s.RoomId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.Doctor)
                    .WithMany(d => d.DoctorTimeSlots)
                    .HasForeignKey(s => s.DoctorId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.ToTable(t =>
                    t.HasCheckConstraint(
                        "CK_Appointments_Status",
                        "\"Status\" IN ('Confirmed', 'Completed', 'Cancelled')"));
                entity.HasKey(a => a.AppointmentId);
                entity.Property(a => a.AppointmentNumber).IsRequired();
                entity.Property(a => a.PatientName).IsRequired().HasMaxLength(150);
                entity.Property(a => a.PatientPhone).IsRequired().HasMaxLength(30);
                entity.Property(a => a.PatientEmail).HasMaxLength(200);
                entity.Property(a => a.AppointmentType).IsRequired().HasMaxLength(80);
                entity.Property(a => a.Reason).IsRequired().HasMaxLength(500);
                entity.Property(a => a.Status).IsRequired().HasMaxLength(30);
                entity.Property(a => a.CancellationReason).HasMaxLength(500);
                entity.Property(a => a.Notes).HasMaxLength(500);
                entity.HasIndex(a => a.Status);
                entity.HasIndex(a => a.CreatedAt);
                entity.HasIndex(a => new { a.DoctorTimeSlotId, a.AppointmentNumber })
                    .IsUnique()
                    .HasFilter("\"Status\" <> 'Cancelled'");
                entity.HasOne(a => a.DoctorTimeSlot)
                    .WithMany(s => s.Appointments)
                    .HasForeignKey(a => a.DoctorTimeSlotId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Patient)
                    .WithMany()
                    .HasForeignKey(a => a.PatientId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<AppointmentProposal>(entity =>
            {
                entity.HasKey(p => p.AppointmentProposalId);
                entity.Property(p => p.CandidateSlotsJson).IsRequired();
                entity.Property(p => p.TriageLevel).IsRequired().HasMaxLength(40);
                entity.Property(p => p.Status).IsRequired().HasMaxLength(40);
                entity.HasIndex(p => new { p.PatientId, p.Status, p.ExpiresAt });
                entity.HasOne<Patient>().WithMany().HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PatientCareAssessment>(entity =>
            {
                entity.HasKey(x => x.PatientCareAssessmentId);
                entity.Property(x => x.Symptoms).IsRequired().HasMaxLength(4000);
                entity.Property(x => x.RequestedSpecialty).HasMaxLength(100);
                entity.Property(x => x.TriageLevel).IsRequired().HasMaxLength(40);
                entity.Property(x => x.Status).IsRequired().HasMaxLength(40);
                entity.Property(x => x.ClinicalJson).IsRequired();
                entity.HasIndex(x => new { x.PatientId, x.CreatedAt });
                entity.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TriageWorkflow>(entity =>
            {
                entity.HasKey(workflow => workflow.TriageWorkflowId);
                entity.Property(workflow => workflow.Status).IsRequired().HasMaxLength(40);
                entity.Property(workflow => workflow.ApprovalStatus).IsRequired().HasMaxLength(40);
                entity.Property(workflow => workflow.TriageLevel).IsRequired().HasMaxLength(40);
                entity.Property(workflow => workflow.UncertaintyState).IsRequired().HasMaxLength(60);
                entity.Property(workflow => workflow.Symptoms).IsRequired().HasMaxLength(4000);
                entity.Property(workflow => workflow.PlanJson).IsRequired();
                entity.Property(workflow => workflow.ResultJson).IsRequired();
                entity.Property(workflow => workflow.RuleSetVersion).IsRequired().HasMaxLength(100);
                entity.Property(workflow => workflow.WorkflowVersion).IsRequired().HasMaxLength(100);
                entity.HasIndex(workflow => new { workflow.PatientId, workflow.CreatedAt });
                entity.Property(workflow => workflow.PriorityLevel).IsRequired().HasMaxLength(40).HasDefaultValue("Normal");
                entity.Property(workflow => workflow.TargetSpecialty).HasMaxLength(100);
                entity.HasOne(workflow => workflow.Patient).WithMany().HasForeignKey(workflow => workflow.PatientId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(workflow => workflow.ReviewedByUser).WithMany().HasForeignKey(workflow => workflow.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(workflow => workflow.AssignedDoctor).WithMany().HasForeignKey(workflow => workflow.AssignedDoctorId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<TriageWorkflowEvent>(entity =>
            {
                entity.HasKey(evt => evt.TriageWorkflowEventId);
                entity.Property(evt => evt.Stage).IsRequired().HasMaxLength(100);
                entity.Property(evt => evt.EventType).IsRequired().HasMaxLength(100);
                entity.Property(evt => evt.DetailsJson).IsRequired();
                entity.HasIndex(evt => new { evt.TriageWorkflowId, evt.CreatedAt });
                entity.HasOne(evt => evt.TriageWorkflow).WithMany().HasForeignKey(evt => evt.TriageWorkflowId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
