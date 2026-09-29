namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

public sealed record AgentDoctor(string Reference, int DoctorId, string Name, string Specialty);
public sealed record AgentSlot(
    string Reference, int DoctorTimeSlotId, int DoctorId, string DoctorName, string Specialty,
    DateTimeOffset StartAt, DateTimeOffset EndAt, int AppointmentNumber,
    int AvailableCount, decimal ConsultationFee, string Location);
public sealed record AgentBooking(int AppointmentId, int DoctorTimeSlotId, int AppointmentNumber,
    string DoctorName, DateTimeOffset StartAt, string Status);
