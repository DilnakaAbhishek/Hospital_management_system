using System.Security.Claims;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Doctor")]
[Route("api/doctor/schedules")]
public class DoctorScheduleController : ControllerBase
{
    private readonly IDoctorScheduleService _service;
    public DoctorScheduleController(IDoctorScheduleService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetMine() => Ok(await _service.GetMineAsync(UserId()));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var schedule = await _service.GetMineByIdAsync(UserId(), id);
        return schedule is null ? NotFound(new { message = "Schedule was not found." }) : Ok(schedule);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDoctorScheduleDto dto)
    {
        try
        {
            var schedule = await _service.CreateAsync(UserId(), dto);
            return CreatedAtAction(nameof(GetById), new { id = schedule.DoctorTimeSlotId }, schedule);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateDoctorScheduleDto dto)
    {
        try
        {
            var schedule = await _service.UpdateAsync(UserId(), id, dto);
            return schedule is null ? NotFound(new { message = "Schedule was not found." }) : Ok(schedule);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var schedule = await _service.CancelAsync(UserId(), id);
            return schedule is null ? NotFound(new { message = "Schedule was not found." }) : Ok(schedule);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await _service.DeleteAsync(UserId(), id)
                ? NoContent()
                : NotFound(new { message = "Schedule was not found." });
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
