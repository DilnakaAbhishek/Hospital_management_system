using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Doctor,Admin")]
[Route("api/rooms")]
public class RoomAvailabilityController : ControllerBase
{
    private readonly IRoomService _service;
    public RoomAvailabilityController(IRoomService service) => _service = service;

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable([FromQuery] DateTime startAt, [FromQuery] DateTime endAt, [FromQuery] int? excludeScheduleId)
    {
        try
        {
            var rooms = await _service.GetAllAsync(startAt, endAt, confirmedOnly: true, excludeScheduleId);
            return Ok(rooms.Where(room => room.Status == "Available"));
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
