using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPatientService _patientService;
    private readonly IDoctorService _doctorService;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher, IPatientService patientService,
        IDoctorService doctorService, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _patientService = patientService;
        _doctorService = doctorService;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.Include(u => u.DoctorProfile).SingleOrDefaultAsync(u => u.Email == email);
        if (user is null ||
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        if (user.Role == "Doctor" && user.DoctorProfile?.RegistrationStatus != DoctorRegistrationStatuses.Approved)
        {
            var message = user.DoctorProfile?.RegistrationStatus == DoctorRegistrationStatuses.Declined
                ? "Your doctor registration was declined. Contact the administrator for assistance."
                : "Your doctor registration is awaiting administrator approval.";
            return StatusCode(StatusCodes.Status403Forbidden, new { message, status = user.DoctorProfile?.RegistrationStatus ?? "Pending" });
        }

        if (Request.Headers.TryGetValue("X-Client-Platform", out var clientPlatform) &&
            string.Equals(clientPlatform.ToString(), "mobile", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Role != "Patient")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Access restricted: Admin and Doctor accounts are only permitted to log in via the web portal. The mobile application is reserved for patients."
                });
            }
        }

        var token = _jwtTokenService.Create(user, user.DoctorProfile?.DoctorId);
        return Ok(new LoginResponseDto { Token = token.Token, ExpiresAt = token.ExpiresAt, UserId = user.UserId,
            DoctorId = user.DoctorProfile?.DoctorId, FullName = user.FullName, Email = user.Email, Role = user.Role });
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponseDto>> Register(RegisterPatientDto dto)
    {
        var email = dto.Email!.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(user => user.Email == email))
            return Conflict(new { message = "An account already exists for this email." });

        try
        {
            await _patientService.CreatePatientAsync(dto);
            var user = new User { FullName = $"{dto.FirstName.Trim()} {dto.LastName.Trim()}", Email = email, Role = "Patient" };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            var token = _jwtTokenService.Create(user, null);
            return Created("api/auth/login", new LoginResponseDto { Token = token.Token, ExpiresAt = token.ExpiresAt,
                UserId = user.UserId, FullName = user.FullName, Email = user.Email, Role = user.Role });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    [HttpPost("doctor-register")]
    public async Task<ActionResult<DoctorRegistrationResponseDto>> RegisterDoctor(RegisterDoctorDto dto)
    {
        try
        {
            await _doctorService.RegisterAsync(dto);
            return StatusCode(StatusCodes.Status201Created, new DoctorRegistrationResponseDto
            {
                Message = "Your registration request was submitted for administrator approval.",
                Status = DoctorRegistrationStatuses.Pending
            });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null)
            return NotFound(new { message = "User not found." });

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Incorrect current password." });

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Password updated successfully." });
    }
}
