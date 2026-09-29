using System.Security.Claims;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/rooms")]
public class RoomAdminController : ControllerBase
{
    private readonly IRoomService _service;
    public RoomAdminController(IRoomService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? startAt, [FromQuery] DateTime? endAt)
    {
        try { return Ok(await _service.GetAllAsync(startAt, endAt)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var room = await _service.GetByIdAsync(id);
        return room is null ? NotFound(new { message = "Room was not found." }) : Ok(room);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoomDto dto)
    {
        try
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var room = await _service.CreateAsync(dto, adminId);
            return CreatedAtAction(nameof(GetById), new { id = room.RoomId }, room);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateRoomDto dto)
    {
        try
        {
            var room = await _service.UpdateAsync(id, dto);
            return room is null ? NotFound(new { message = "Room was not found." }) : Ok(room);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id)
    {
        var room = await _service.ConfirmAsync(id);
        return room is null ? NotFound(new { message = "Room was not found." }) : Ok(room);
    }
}
