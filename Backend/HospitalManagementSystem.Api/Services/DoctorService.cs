using System.Text.RegularExpressions;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Services;

public partial class DoctorService : IDoctorService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DoctorService(ApplicationDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task RegisterAsync(RegisterDoctorDto dto)
    {
        var firstName = ValidateName(dto.FirstName, "First name");
        var lastName = ValidateName(dto.LastName, "Last name");
        var email = dto.Email.Trim().ToLowerInvariant();
        var nic = dto.NIC.Trim().ToUpperInvariant();
        var license = dto.SlmcLicenseNumber.Trim().ToUpperInvariant();
        var phone = NormalizePhone(dto.PhoneNumber);

        if (!NicRegex().IsMatch(nic))
            throw new ArgumentException("NIC must use the Sri Lankan old format (9 digits followed by V/X) or new 12-digit format.");
        if (!PasswordLetterRegex().IsMatch(dto.Password) || !PasswordDigitRegex().IsMatch(dto.Password))
            throw new ArgumentException("Password must be at least 8 characters and contain at least one letter and one number.");
        if (string.IsNullOrWhiteSpace(dto.Specialization))
            throw new ArgumentException("Specialization is required.");
        if (!LicenseRegex().IsMatch(license))
            throw new ArgumentException("SLMC license number may contain only letters, numbers, spaces and hyphens.");
        if (await _db.Users.AnyAsync(user => user.Email == email))
            throw new InvalidOperationException("An account already exists for this email.");
        if (await _db.Doctors.AnyAsync(doctor => doctor.NIC == nic))
            throw new InvalidOperationException("A doctor registration already exists for this NIC.");
        if (await _db.Doctors.AnyAsync(doctor => doctor.SlmcLicenseNumber == license))
            throw new InvalidOperationException("A doctor registration already exists for this SLMC license number.");

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(_db, async (context, ct) =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var user = new User { FullName = $"{firstName} {lastName}", Email = email, Role = "Doctor" };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            context.Users.Add(user);
            context.Doctors.Add(new Doctor
            {
                User = user,
                FirstName = firstName,
                LastName = lastName,
                NIC = nic,
                Specialization = dto.Specialization.Trim(),
                SlmcLicenseNumber = license,
                PhoneNumber = phone,
                RegistrationStatus = DoctorRegistrationStatuses.Pending
            });
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }, default);
    }

    public async Task<IEnumerable<DoctorDto>> GetRegistrationsAsync(string? status)
    {
        var query = _db.Doctors.Include(d => d.User).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = NormalizeStatus(status);
            query = query.Where(d => d.RegistrationStatus == normalized);
        }
        return (await query.OrderByDescending(d => d.CreatedAt).ToListAsync()).Select(Map);
    }

    public async Task<DoctorDto?> GetByIdAsync(int doctorId)
    {
        var doctor = await _db.Doctors.Include(d => d.User).AsNoTracking().SingleOrDefaultAsync(d => d.DoctorId == doctorId);
        return doctor is null ? null : Map(doctor);
    }

    public async Task<DoctorDto?> ReviewAsync(int doctorId, int adminUserId, bool approve, string? declineReason)
    {
        var doctor = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.DoctorId == doctorId);
        if (doctor is null) return null;
        if (doctor.RegistrationStatus != DoctorRegistrationStatuses.Pending)
            throw new InvalidOperationException("Only pending doctor registrations can be reviewed.");

        doctor.RegistrationStatus = approve ? DoctorRegistrationStatuses.Approved : DoctorRegistrationStatuses.Declined;
        doctor.DeclineReason = approve ? null : declineReason?.Trim();
        doctor.ReviewedByUserId = adminUserId;
        doctor.ReviewedAt = DateTime.UtcNow;
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(doctor);
    }

    public async Task<DoctorDto?> GetByUserIdAsync(int userId)
    {
        var doctor = await _db.Doctors.Include(d => d.User).AsNoTracking().SingleOrDefaultAsync(d => d.UserId == userId);
        return doctor is null ? null : Map(doctor);
    }

    public async Task<DoctorDto?> UpdateProfileAsync(int userId, UpdateDoctorProfileDto dto)
    {
        var doctor = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.UserId == userId);
        if (doctor is null) return null;
        var previousDoctorName = $"Dr. {doctor.FirstName} {doctor.LastName}".Trim();
        doctor.FirstName = ValidateName(dto.FirstName, "First name");
        doctor.LastName = ValidateName(dto.LastName, "Last name");
        doctor.PhoneNumber = NormalizePhone(dto.PhoneNumber);
        doctor.User.FullName = $"{doctor.FirstName} {doctor.LastName}";
        doctor.UpdatedAt = DateTime.UtcNow;

        // DoctorTimeSlots keeps a display-name copy. Refresh it after a profile
        // name change, but deliberately leave Specialty untouched because doctors
        // are not allowed to edit their verified specialization.
        var updatedDoctorName = $"Dr. {doctor.FirstName} {doctor.LastName}".Trim();
        var now = DateTime.UtcNow;
        var matchingSlots = await _db.DoctorTimeSlots
            .Where(slot => slot.DoctorId == doctor.DoctorId || slot.DoctorName == previousDoctorName)
            .ToListAsync();
        foreach (var slot in matchingSlots)
        {
            slot.DoctorId ??= doctor.DoctorId;
            slot.DoctorName = updatedDoctorName;
            slot.UpdatedAt = now;
        }
        await _db.SaveChangesAsync();
        return Map(doctor);
    }

    public async Task<IReadOnlyList<DoctorSearchResultDto>> SearchApprovedDoctorsAsync(string? query, int limit = 20)
    {
        var term = query?.Trim() ?? string.Empty;
        if (term.Length > 100)
            throw new ArgumentException("Search text cannot exceed 100 characters.");

        var take = Math.Clamp(limit, 1, 50);
        var doctors = _db.Doctors
            .AsNoTracking()
            .Where(doctor => doctor.RegistrationStatus == DoctorRegistrationStatuses.Approved);

        if (term.Length > 0)
        {
            var normalizedTerm = term.ToLower();
            doctors = doctors.Where(doctor =>
                doctor.FirstName.ToLower().Contains(normalizedTerm) ||
                doctor.LastName.ToLower().Contains(normalizedTerm) ||
                (doctor.FirstName + " " + doctor.LastName).ToLower().Contains(normalizedTerm) ||
                doctor.Specialization.ToLower().Contains(normalizedTerm));
        }

        return await doctors
            .OrderBy(doctor => doctor.FirstName)
            .ThenBy(doctor => doctor.LastName)
            .Take(take)
            .Select(doctor => new DoctorSearchResultDto
            {
                DoctorId = doctor.DoctorId,
                FullName = doctor.FirstName + " " + doctor.LastName,
                Specialization = doctor.Specialization
            })
            .ToListAsync();
    }

    public Task<DoctorPublicProfileDto?> GetApprovedDoctorProfileAsync(int doctorId) =>
        _db.Doctors
            .AsNoTracking()
            .Where(doctor => doctor.DoctorId == doctorId &&
                             doctor.RegistrationStatus == DoctorRegistrationStatuses.Approved)
            .Select(doctor => new DoctorPublicProfileDto
            {
                DoctorId = doctor.DoctorId,
                FullName = doctor.FirstName + " " + doctor.LastName,
                Email = doctor.User.Email,
                SlmcLicenseNumber = doctor.SlmcLicenseNumber,
                Specialization = doctor.Specialization
            })
            .SingleOrDefaultAsync();

    public async Task<DoctorDto?> RequestDeletionAsync(int userId, string? reason)
    {
        var doctor = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.UserId == userId);
        if (doctor is null) return null;
        if (doctor.RegistrationStatus != DoctorRegistrationStatuses.Approved)
            throw new InvalidOperationException("Only active approved doctors can request account deletion.");

        var now = DateTime.UtcNow;
        var hasActiveBookings = await _db.Appointments
            .Include(a => a.DoctorTimeSlot)
            .AnyAsync(a =>
                (a.DoctorTimeSlot!.DoctorId == doctor.DoctorId ||
                 a.DoctorTimeSlot.DoctorName.ToLower() == (doctor.FirstName + " " + doctor.LastName).ToLower() ||
                 a.DoctorTimeSlot.DoctorName.ToLower() == ("Dr. " + doctor.FirstName + " " + doctor.LastName).ToLower()) &&
                a.Status == "Confirmed" &&
                a.DoctorTimeSlot.StartAt > now);

        if (hasActiveBookings)
        {
            throw new InvalidOperationException("Cannot request account deletion while you have upcoming confirmed patient appointments. Please cancel or reassign scheduled appointments first.");
        }

        var hasUpcomingSlots = await _db.DoctorTimeSlots
            .AnyAsync(s =>
                (s.DoctorId == doctor.DoctorId ||
                 s.DoctorName.ToLower() == (doctor.FirstName + " " + doctor.LastName).ToLower() ||
                 s.DoctorName.ToLower() == ("Dr. " + doctor.FirstName + " " + doctor.LastName).ToLower()) &&
                s.IsActive &&
                s.StartAt > now);

        if (hasUpcomingSlots)
        {
            throw new InvalidOperationException("Cannot request account deletion while you have active upcoming appointment slots. Please remove or cancel your scheduled slots first.");
        }

        doctor.RegistrationStatus = DoctorRegistrationStatuses.DeletionPending;
        doctor.DeclineReason = string.IsNullOrWhiteSpace(reason) ? "Doctor requested account removal." : reason.Trim();
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(doctor);
    }

    public async Task<bool> ApproveDeletionAsync(int doctorId, int adminUserId)
    {
        var doctor = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.DoctorId == doctorId);
        if (doctor is null) return false;
        if (doctor.RegistrationStatus != DoctorRegistrationStatuses.DeletionPending)
            throw new InvalidOperationException("Only doctors with pending deletion requests can be deleted.");

        var now = DateTime.UtcNow;
        var hasActiveBookings = await _db.Appointments
            .Include(a => a.DoctorTimeSlot)
            .AnyAsync(a =>
                (a.DoctorTimeSlot!.DoctorId == doctor.DoctorId ||
                 a.DoctorTimeSlot.DoctorName.ToLower() == (doctor.FirstName + " " + doctor.LastName).ToLower() ||
                 a.DoctorTimeSlot.DoctorName.ToLower() == ("Dr. " + doctor.FirstName + " " + doctor.LastName).ToLower()) &&
                a.Status == "Confirmed" &&
                a.DoctorTimeSlot.StartAt > now);

        if (hasActiveBookings)
        {
            throw new InvalidOperationException("Cannot approve deletion: Doctor has upcoming confirmed patient appointments.");
        }

        var hasUpcomingSlots = await _db.DoctorTimeSlots
            .AnyAsync(s =>
                (s.DoctorId == doctor.DoctorId ||
                 s.DoctorName.ToLower() == (doctor.FirstName + " " + doctor.LastName).ToLower() ||
                 s.DoctorName.ToLower() == ("Dr. " + doctor.FirstName + " " + doctor.LastName).ToLower()) &&
                s.IsActive &&
                s.StartAt > now);

        if (hasUpcomingSlots)
        {
            throw new InvalidOperationException("Cannot approve deletion: Doctor has active upcoming appointment slots.");
        }

        _db.Users.Remove(doctor.User);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<DoctorDto?> CancelDeletionByDoctorAsync(int userId)
    {
        var doctor = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.UserId == userId);
        if (doctor is null) return null;
        if (doctor.RegistrationStatus != DoctorRegistrationStatuses.DeletionPending)
            throw new InvalidOperationException("Doctor does not have a pending deletion request.");

        doctor.RegistrationStatus = DoctorRegistrationStatuses.Approved;
        doctor.DeclineReason = null;
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(doctor);
    }

    public async Task<DoctorDto?> CancelDeletionAsync(int doctorId, int adminUserId, string? reason = null)
    {
        var doctor = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.DoctorId == doctorId);
        if (doctor is null) return null;
        if (doctor.RegistrationStatus != DoctorRegistrationStatuses.DeletionPending)
            throw new InvalidOperationException("Only doctors with pending deletion requests can have deletion cancelled.");

        doctor.RegistrationStatus = DoctorRegistrationStatuses.Approved;
        doctor.DeclineReason = null;
        doctor.ReviewedByUserId = adminUserId;
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(doctor);
    }

    private static string NormalizeStatus(string status)
    {
        var value = status.Trim();
        return value.ToLowerInvariant() switch
        {
            "pending" or "pendingapproval" or "pending_approval" => DoctorRegistrationStatuses.Pending,
            "approved" => DoctorRegistrationStatuses.Approved,
            "declined" => DoctorRegistrationStatuses.Declined,
            "deletionpending" or "deletion_pending" or "deletion" => DoctorRegistrationStatuses.DeletionPending,
            _ => throw new ArgumentException("Registration status must be Pending, Approved, Declined, or DeletionPending.")
        };
    }

    private static string ValidateName(string value, string label)
    {
        var name = value.Trim();
        if (!NameRegex().IsMatch(name))
            throw new ArgumentException($"{label} may contain only letters, spaces, apostrophes and hyphens.");
        return name;
    }

    private static string NormalizePhone(string value)
    {
        var compact = value.Replace(" ", "").Replace("-", "");
        if (compact.StartsWith("+94")) compact = $"0{compact[3..]}";
        if (!PhoneRegex().IsMatch(compact))
            throw new ArgumentException("Phone number must be a valid Sri Lankan mobile number (07XXXXXXXX or +947XXXXXXXX). ");
        return compact;
    }

    private static DoctorDto Map(Doctor doctor) => new()
    {
        DoctorId = doctor.DoctorId, UserId = doctor.UserId, FirstName = doctor.FirstName,
        LastName = doctor.LastName, Email = doctor.User.Email, NIC = doctor.NIC,
        Specialization = doctor.Specialization, SlmcLicenseNumber = doctor.SlmcLicenseNumber,
        PhoneNumber = doctor.PhoneNumber, RegistrationStatus = doctor.RegistrationStatus,
        DeclineReason = doctor.DeclineReason, CreatedAt = doctor.CreatedAt, ReviewedAt = doctor.ReviewedAt
    };

    [GeneratedRegex(@"^(?:\d{9}[VX]|\d{12})$", RegexOptions.IgnoreCase)] private static partial Regex NicRegex();
    [GeneratedRegex(@"^07\d{8}$")] private static partial Regex PhoneRegex();
    [GeneratedRegex(@"^[\p{L}][\p{L} '\-]*$")] private static partial Regex NameRegex();
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9 \-]{2,49}$", RegexOptions.IgnoreCase)] private static partial Regex LicenseRegex();
    [GeneratedRegex(@"[A-Za-z]")] private static partial Regex PasswordLetterRegex();
    [GeneratedRegex(@"\d")] private static partial Regex PasswordDigitRegex();
}
