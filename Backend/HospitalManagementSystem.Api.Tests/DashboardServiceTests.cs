using System;
using System.Linq;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HospitalManagementSystem.Api.Tests
{
    public class DashboardServiceTests
    {
        [Fact]
        public async Task GetAdminDashboardStatsAsync_ReturnsAccurateMetricsAndTrends()
        {
            await using var db = CreateContext();

            // Seed Patients
            var patient1 = new Patient
            {
                FirstName = "Amal",
                LastName = "Perera",
                Email = "amal@example.com",
                NIC = "850314123V",
                Gender = "Male",
                PhoneNumber = "+94772345678",
                BloodGroup = "A+",
                CreatedAt = DateTime.UtcNow
            };
            var patient2 = new Patient
            {
                FirstName = "Nimesha",
                LastName = "Silva",
                Email = "nimesha@example.com",
                NIC = "920722234V",
                Gender = "Female",
                PhoneNumber = "+94763456789",
                BloodGroup = "O+",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };
            db.Patients.AddRange(patient1, patient2);

            // Seed Doctors
            var user1 = new User { FullName = "Dr. Nimal", Email = "doc1@hospital.com", PasswordHash = "hash", Role = "Doctor" };
            var doctor1 = new Doctor
            {
                User = user1,
                FirstName = "Nimal",
                LastName = "Perera",
                NIC = "198012345678",
                Specialization = "Cardiology",
                SlmcLicenseNumber = "SLMC-101",
                PhoneNumber = "0771234567",
                RegistrationStatus = "Approved",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };
            var user2 = new User { FullName = "Dr. Kamal", Email = "doc2@hospital.com", PasswordHash = "hash", Role = "Doctor" };
            var doctor2 = new Doctor
            {
                User = user2,
                FirstName = "Kamal",
                LastName = "Silva",
                NIC = "198512345678",
                Specialization = "Neurology",
                SlmcLicenseNumber = "SLMC-102",
                PhoneNumber = "0777654321",
                RegistrationStatus = "Pending",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };
            db.Doctors.AddRange(doctor1, doctor2);

            // Seed TimeSlot & Appointment
            var room = new Room { RoomNumber = "R101", RoomName = "Consultation 1", Floor = "1", IsConfirmed = true };
            db.Rooms.Add(room);
            await db.SaveChangesAsync();

            var slot = new DoctorTimeSlot
            {
                DoctorId = doctor1.DoctorId,
                DoctorName = "Dr. Nimal Perera",
                Specialty = "Cardiology",
                RoomId = room.RoomId,
                StartAt = DateTime.UtcNow.Date.AddHours(10),
                EndAt = DateTime.UtcNow.Date.AddHours(11),
                Capacity = 5,
                ConsultationFee = 2500m,
                IsActive = true
            };
            db.DoctorTimeSlots.Add(slot);
            await db.SaveChangesAsync();

            var appointment = new Appointment
            {
                DoctorTimeSlotId = slot.DoctorTimeSlotId,
                PatientId = patient1.PatientId,
                AppointmentNumber = 1,
                PatientName = "Amal Perera",
                PatientPhone = "+94772345678",
                PatientEmail = "amal@example.com",
                AppointmentType = "Consultation",
                Reason = "General checkup",
                Status = "Confirmed",
                CreatedAt = DateTime.UtcNow
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();

            var service = new DashboardService(db);
            var stats = await service.GetAdminDashboardStatsAsync();

            Assert.NotNull(stats);
            Assert.Equal("2", stats.PatientsStat.Value);
            Assert.Equal("1", stats.DoctorsStat.Value); // Only 1 Approved doctor
            Assert.Equal("1", stats.AppointmentsStat.Value); // 1 today
            Assert.Equal("Rs 2.5K", stats.RevenueStat.Value); // 2500 formatted as Rs 2.5K

            Assert.Equal(6, stats.MonthlyTrends.Count);
            Assert.NotEmpty(stats.RecentPatients);
            Assert.Equal(2, stats.RecentPatients.Count);
        }

        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }
    }
}
