using System;
using System.Collections.Generic;

namespace HospitalManagementSystem.Api.DTOs
{
    public class AdminDashboardDto
    {
        public DashboardStatDto PatientsStat { get; set; } = new();
        public DashboardStatDto DoctorsStat { get; set; } = new();
        public DashboardStatDto AppointmentsStat { get; set; } = new();
        public DashboardStatDto RevenueStat { get; set; } = new();

        public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();
        public List<PatientDto> RecentPatients { get; set; } = new();
    }

    public class DashboardStatDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Change { get; set; } = string.Empty;
        public bool Up { get; set; } = true;
    }

    public class MonthlyTrendDto
    {
        public string Month { get; set; } = string.Empty;
        public int Patients { get; set; }
        public int Appointments { get; set; }
    }
}
