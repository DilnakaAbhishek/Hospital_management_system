using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Route("api/patient/doctors")]
[Authorize(Roles = "Patient,Admin,Doctor")]
public sealed class PatientDoctorController : ControllerBase
{
    private readonly IDoctorService _doctorService;

    public PatientDoctorController(IDoctorService doctorService) => _doctorService = doctorService;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] int limit = 20)
    {
        try
        {
            return Ok(await _doctorService.SearchApprovedDoctorsAsync(query, limit));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{doctorId:int}")]
    [ProducesResponseType(typeof(DoctorPublicProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(int doctorId)
    {
        var doctor = await _doctorService.GetApprovedDoctorProfileAsync(doctorId);
        return doctor is null
            ? NotFound(new { message = "Approved doctor was not found." })
            : Ok(doctor);
    }
}
