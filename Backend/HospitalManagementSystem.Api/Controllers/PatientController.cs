using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PatientController : ControllerBase
    {
        private readonly IPatientService _service;
        private readonly ILogger<PatientController> _logger;

        public PatientController(IPatientService service, ILogger<PatientController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET /api/patient?search=john&gender=Female&bloodGroup=O%2B&sortBy=name&sortDirection=asc&page=1&pageSize=10
        [HttpGet]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(typeof(PagedResult<PatientDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? gender,
            [FromQuery] string? bloodGroup,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetAllPatientsAsync(search, gender, bloodGroup, sortBy, sortDirection, page, pageSize);
            return Ok(result);
        }

        // GET /api/patient/summary
        [HttpGet("summary")]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(typeof(PatientSummaryDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSummary() => Ok(await _service.GetSummaryAsync());

        // GET /api/patient/me
        [HttpGet("me")]
        [Authorize(Roles = "Patient")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyProfile()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized(new { message = "The access token does not contain a user email." });

            var patient = await _service.GetPatientByEmailAsync(email);
            if (patient is null)
                return NotFound(new { message = "No patient profile found for this account." });

            return Ok(patient);
        }

        // PUT /api/patient/me
        [HttpPut("me")]
        [Authorize(Roles = "Patient")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdatePatientDto dto)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized(new { message = "The access token does not contain a user email." });

            if (!string.Equals(dto.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Patient email cannot be changed from this endpoint." });

            var patient = await _service.GetPatientByEmailAsync(email);
            if (patient is null)
                return NotFound(new { message = "No patient profile found for this account." });

            try
            {
                var updated = await _service.UpdatePatientAsync(patient.PatientId, dto);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Conflict updating own patient profile: {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
        }

        // GET /api/patient/{id}
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var patient = await _service.GetPatientByIdAsync(id);
            if (patient is null)
            {
                _logger.LogWarning("Patient with ID {Id} not found.", id);
                return NotFound(new { message = $"Patient with ID {id} not found." });
            }
            return Ok(patient);
        }

        // GET /api/patient/{id}/appointments
        [HttpGet("{id:int}/appointments")]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(typeof(IEnumerable<PatientAppointmentHistoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAppointmentHistory(int id)
        {
            var appointments = await _service.GetAppointmentHistoryAsync(id);
            if (appointments is null)
                return NotFound(new { message = $"Patient with ID {id} not found." });

            return Ok(appointments);
        }

        // POST /api/patient
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreatePatientDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var created = await _service.CreatePatientAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.PatientId }, created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Conflict creating patient: {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
        }

        // PUT /api/patient/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePatientDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var updated = await _service.UpdatePatientAsync(id, dto);
                if (updated is null)
                    return NotFound(new { message = $"Patient with ID {id} not found." });

                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Conflict updating patient {Id}: {Message}", id, ex.Message);
                return Conflict(new { message = ex.Message });
            }
        }

        // DELETE /api/patient/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeletePatientAsync(id);
            if (!deleted)
                return NotFound(new { message = $"Patient with ID {id} not found." });

            return NoContent();
        }
    }
}
