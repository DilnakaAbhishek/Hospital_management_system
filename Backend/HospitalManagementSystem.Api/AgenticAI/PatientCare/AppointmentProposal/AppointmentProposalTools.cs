using System.ComponentModel.DataAnnotations;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

public interface IAppointmentSearchTools
{
    Task<IReadOnlyList<AgentDoctor>> FindDoctorsAsync(string query);
    Task<IReadOnlyList<AgentSlot>> FindSlotsAsync(AgentDoctor doctor, DateOnly? date);
}

public interface IAppointmentBookingTools
{
    Task<AgentBooking> BookAsync(AgentSlot observedSlot, PatientDto patient);
}

public interface IAppointmentAgentTools : IAppointmentSearchTools, IAppointmentBookingTools { }

// No repositories or DbContext: Doctor Management owns doctor eligibility, and
// AppointmentService owns available numbers, session times, and booking rules.
public sealed class AppointmentAgentTools(IDoctorService doctors, IAppointmentService appointments) : IAppointmentAgentTools
{
    public static readonly TimeZoneInfo HospitalTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");

    public async Task<IReadOnlyList<AgentDoctor>> FindDoctorsAsync(string query)
    {
        var approved = await doctors.GetRegistrationsAsync(DoctorRegistrationStatuses.Approved);
        return approved.Where(d => string.IsNullOrWhiteSpace(query) ||
                $"Dr. {d.FullName}".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.Specialization.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.FullName).Take(20)
            .Select(d => new AgentDoctor($"D{d.DoctorId}", d.DoctorId, $"Dr. {d.FullName}", d.Specialization)).ToList();
    }

    public async Task<IReadOnlyList<AgentSlot>> FindSlotsAsync(AgentDoctor doctor, DateOnly? date)
    {
        await RequireApprovedDoctorAsync(doctor.DoctorId);
        // The existing date filter is UTC; filter its results by Sri Lanka date here
        // so appointments near midnight are not assigned to the wrong local day.
        var slots = await appointments.GetSlotsAsync(null, null, true, doctor.DoctorId);
        return slots.Where(s => s.DoctorId == doctor.DoctorId && s.IsActive &&
                s.StartAt > DateTime.UtcNow && s.AvailableCount > 0 && s.NextAppointmentNumber > 0 &&
                (!date.HasValue || DateOnly.FromDateTime(Local(s.StartAt).DateTime) == date.Value))
            .OrderBy(s => s.StartAt).Take(12).Select(MapSlot).ToList();
    }

    public async Task<AgentBooking> BookAsync(AgentSlot observedSlot, PatientDto patient)
    {
        var fresh = await FindSlotsAsync(new AgentDoctor($"D{observedSlot.DoctorId}", observedSlot.DoctorId, observedSlot.DoctorName, observedSlot.Specialty),
            DateOnly.FromDateTime(observedSlot.StartAt.DateTime));
        var slot = fresh.SingleOrDefault(s => s.DoctorTimeSlotId == observedSlot.DoctorTimeSlotId);
        if (slot is null || slot != observedSlot)
            throw new InvalidOperationException("The selected slot changed or is no longer available. Search again for current options.");

        var dto = new CreateAppointmentDto
        {
            DoctorTimeSlotId = slot.DoctorTimeSlotId,
            PatientId = patient.PatientId, PatientName = patient.FullName,
            PatientEmail = patient.Email, PatientPhone = patient.PhoneNumber,
            AppointmentType = "Consultation"
        };
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true))
            throw new InvalidOperationException("The patient profile needs valid contact details before booking.");
        var booked = await appointments.CreateAppointmentAsync(dto);
        return new AgentBooking(booked.AppointmentId, booked.DoctorTimeSlotId, booked.AppointmentNumber,
            booked.DoctorName, Local(booked.StartAt), booked.Status);
    }

    private async Task<DoctorDto> RequireApprovedDoctorAsync(int id)
    {
        var doctor = await doctors.GetByIdAsync(id);
        if (doctor is null || doctor.RegistrationStatus != DoctorRegistrationStatuses.Approved)
            throw new InvalidOperationException("The doctor is no longer available for appointments.");
        return doctor;
    }

    public static DateTimeOffset Local(DateTime value) => TimeZoneInfo.ConvertTime(
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)), HospitalTimeZone);

    private static AgentSlot MapSlot(DoctorTimeSlotDto s) => new($"S{s.DoctorTimeSlotId}", s.DoctorTimeSlotId,
        s.DoctorId!.Value, s.DoctorName, s.Specialty, Local(s.StartAt), Local(s.EndAt), s.NextAppointmentNumber,
        s.AvailableCount, s.ConsultationFee,
        string.Join(", ", new[] { s.RoomNumber, s.RoomName, s.Floor }.Where(v => !string.IsNullOrWhiteSpace(v))));
}
