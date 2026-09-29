using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _db;

        public DashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<AdminDashboardDto> GetAdminDashboardStatsAsync()
        {
            var now = DateTime.UtcNow;
            var todayUtc = now.Date;
            var yesterdayUtc = todayUtc.AddDays(-1);
            var minDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-6);

            // 1. Overall counts in single database queries
            var totalPatients = await _db.Patients.AsNoTracking().CountAsync();
            var activeDoctors = await _db.Doctors.AsNoTracking().CountAsync(d => d.RegistrationStatus == "Approved");
            var newDoctorsThisMonth = await _db.Doctors.AsNoTracking().CountAsync(d => d.RegistrationStatus == "Approved" && d.CreatedAt >= now.AddDays(-30));

            // 2. Fetch patient registration dates for trend and stat calculations in a single query
            var patientDates = await _db.Patients
                .AsNoTracking()
                .Where(p => p.CreatedAt >= minDate.AddDays(-65))
                .Select(p => p.CreatedAt)
                .ToListAsync();

            // 3. Fetch appointment records for trend, today's count, and revenue calculations in a single query
            var appointmentData = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.Status != "Cancelled" && (a.CreatedAt >= minDate || (a.DoctorTimeSlot != null && a.DoctorTimeSlot.StartAt >= minDate)))
                .Select(a => new {
                    CreatedAt = a.CreatedAt,
                    StartAt = a.DoctorTimeSlot != null ? a.DoctorTimeSlot.StartAt : (DateTime?)null,
                    Fee = a.DoctorTimeSlot != null ? a.DoctorTimeSlot.ConsultationFee : 0m
                })
                .ToListAsync();

            // 4. Recent Patients list query
            var recentEntities = await _db.Patients
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Compute metrics in-memory from pre-fetched lists
            var patientsThisMonth = patientDates.Count(d => d >= now.AddDays(-30));
            var patientsPrevMonth = patientDates.Count(d => d >= now.AddDays(-60) && d < now.AddDays(-30));
            var (patientsChangeStr, patientsUp) = CalculatePercentageChange(patientsThisMonth, patientsPrevMonth);

            var doctorsChangeStr = newDoctorsThisMonth > 0 ? $"+{newDoctorsThisMonth}" : "0";

            var todaysAppointments = appointmentData.Count(a => (a.StartAt.HasValue && a.StartAt.Value.Date == todayUtc) || a.CreatedAt.Date == todayUtc);
            var yesterdaysAppointments = appointmentData.Count(a => (a.StartAt.HasValue && a.StartAt.Value.Date == yesterdayUtc) || a.CreatedAt.Date == yesterdayUtc);
            var (appointmentsChangeStr, appointmentsUp) = CalculatePercentageChange(todaysAppointments, yesterdaysAppointments);

            var monthlyRevenue = appointmentData
                .Where(a => a.StartAt.HasValue && a.StartAt.Value.Month == now.Month && a.StartAt.Value.Year == now.Year)
                .Sum(a => a.Fee);

            var prevMonthDate = now.AddMonths(-1);
            var prevMonthlyRevenue = appointmentData
                .Where(a => a.StartAt.HasValue && a.StartAt.Value.Month == prevMonthDate.Month && a.StartAt.Value.Year == prevMonthDate.Year)
                .Sum(a => a.Fee);

            var (revenueChangeStr, revenueUp) = CalculateRevenuePercentageChange(monthlyRevenue, prevMonthlyRevenue);

            var monthlyTrends = new List<MonthlyTrendDto>();
            for (int i = 5; i >= 0; i--)
            {
                var targetMonthDate = now.AddMonths(-i);
                var year = targetMonthDate.Year;
                var month = targetMonthDate.Month;
                var monthName = targetMonthDate.ToString("MMM", CultureInfo.InvariantCulture);

                var monthPatients = patientDates.Count(d => d.Year == year && d.Month == month);
                var monthAppointments = appointmentData.Count(a =>
                    (a.StartAt.HasValue && a.StartAt.Value.Year == year && a.StartAt.Value.Month == month) ||
                    (a.CreatedAt.Year == year && a.CreatedAt.Month == month));

                monthlyTrends.Add(new MonthlyTrendDto
                {
                    Month = monthName,
                    Patients = monthPatients,
                    Appointments = monthAppointments
                });
            }

            var recentPatients = recentEntities.Select(MapToPatientDto).ToList();

            return new AdminDashboardDto
            {
                PatientsStat = new DashboardStatDto
                {
                    Label = "Total Patients",
                    Value = totalPatients.ToString("N0"),
                    Change = patientsChangeStr,
                    Up = patientsUp
                },
                DoctorsStat = new DashboardStatDto
                {
                    Label = "Active Doctors",
                    Value = activeDoctors.ToString("N0"),
                    Change = doctorsChangeStr,
                    Up = true
                },
                AppointmentsStat = new DashboardStatDto
                {
                    Label = "Today's Appointments",
                    Value = todaysAppointments.ToString("N0"),
                    Change = appointmentsChangeStr,
                    Up = appointmentsUp
                },
                RevenueStat = new DashboardStatDto
                {
                    Label = "Monthly Revenue",
                    Value = FormatRevenue(monthlyRevenue),
                    Change = revenueChangeStr,
                    Up = revenueUp
                },
                MonthlyTrends = monthlyTrends,
                RecentPatients = recentPatients
            };
        }

        private static (string ChangeStr, bool Up) CalculatePercentageChange(int current, int previous)
        {
            if (previous == 0)
            {
                return current > 0 ? ("+100%", true) : ("0%", true);
            }

            var diff = current - previous;
            var pct = (diff / (double)previous) * 100;
            var isUp = pct >= 0;
            var sign = isUp ? "+" : "";
            return ($"{sign}{pct:F0}%", isUp);
        }

        private static (string ChangeStr, bool Up) CalculateRevenuePercentageChange(decimal current, decimal previous)
        {
            if (previous == 0m)
            {
                return current > 0m ? ("+100%", true) : ("0%", true);
            }

            var diff = current - previous;
            var pct = (double)(diff / previous) * 100;
            var isUp = pct >= 0;
            var sign = isUp ? "+" : "";
            return ($"{sign}{pct:F0}%", isUp);
        }

        private static string FormatRevenue(decimal revenue)
        {
            if (revenue >= 1_000_000m)
            {
                return $"Rs {(revenue / 1_000_000m):0.#}M";
            }
            if (revenue >= 1_000m)
            {
                return $"Rs {(revenue / 1_000m):0.#}K";
            }
            return $"Rs {revenue:N0}";
        }

        private static PatientDto MapToPatientDto(Patient p) => new()
        {
            PatientId = p.PatientId,
            FirstName = p.FirstName,
            LastName = p.LastName,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            NIC = p.NIC,
            PhoneNumber = p.PhoneNumber,
            Email = p.Email ?? string.Empty,
            Address = p.Address ?? string.Empty,
            BloodGroup = p.BloodGroup ?? string.Empty,
            EmergencyContactName = p.EmergencyContactName ?? string.Empty,
            EmergencyContactPhone = p.EmergencyContactPhone ?? string.Empty,
            ProfileImageUrl = p.ProfileImageUrl,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
