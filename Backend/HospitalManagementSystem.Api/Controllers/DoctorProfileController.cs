using System.Security.Claims;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Doctor")]
[Route("api/doctors/me")]
public class DoctorProfileController : ControllerBase
{
    private readonly IDoctorService _service;
    public DoctorProfileController(IDoctorService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var doctor = await _service.GetByUserIdAsync(CurrentUserId());
        return doctor is null ? NotFound(new { message = "Doctor profile was not found." }) : Ok(doctor);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile(UpdateDoctorProfileDto dto)
    {
        try
        {
            var doctor = await _service.UpdateProfileAsync(CurrentUserId(), dto);
            return doctor is null ? NotFound(new { message = "Doctor profile was not found." }) : Ok(doctor);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("request-deletion")]
    public async Task<IActionResult> RequestDeletion([FromBody] RequestDoctorDeletionDto dto)
    {
        try
        {
            var doctor = await _service.RequestDeletionAsync(CurrentUserId(), dto?.Reason);
            return doctor is null ? NotFound(new { message = "Doctor profile was not found." }) : Ok(doctor);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("cancel-deletion")]
    public async Task<IActionResult> CancelDeletion()
    {
        try
        {
            var doctor = await _service.CancelDeletionByDoctorAsync(CurrentUserId());
            return doctor is null ? NotFound(new { message = "Doctor profile was not found." }) : Ok(doctor);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
