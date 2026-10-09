using System.Security.Claims;
using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    private int GetCurrentPrincipalId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return (claim != null && int.TryParse(claim.Value, out int id)) ? id : 0;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentRequest request)
    {
        // IDOR KILL: Ignore any request.UserId in body, strictly derive from JWT identity (Rule c)
        int userId = GetCurrentPrincipalId();
        if (userId == 0)
        {
            return Unauthorized(new { message = "Valid authenticated customer account is required." });
        }

        try
        {
            var appointment = await _appointmentService.BookAppointmentAsync(userId, request);
            return StatusCode(201, new { message = "Appointment booked successfully!", appointment });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, error = ex.Message });
        }
    }

    [HttpGet("doctor/{doctorId?}")]
    [HttpGet("doctor")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> GetDoctorAppointments(int? doctorId)
    {
        int currentId = GetCurrentPrincipalId();
        var isAdmin = User.IsInRole("Admin");

        // Doctor may ONLY view their own appointments; Admin may view any or all
        int targetDoctorId = isAdmin ? (doctorId ?? 0) : currentId;
        if (targetDoctorId == 0)
        {
            if (isAdmin)
            {
                var all = await _appointmentService.GetAllAppointmentsAsync();
                return Ok(new { success = true, appointments = all });
            }
            return BadRequest(new { message = "Doctor ID is required." });
        }

        var list = await _appointmentService.GetDoctorAppointmentsAsync(targetDoctorId);
        return Ok(new { success = true, appointments = list });
    }

    [HttpGet("user/{userId?}")]
    [HttpGet("user")]
    [Authorize]
    public async Task<IActionResult> GetUserAppointments(int? userId)
    {
        int currentId = GetCurrentPrincipalId();
        var isAdmin = User.IsInRole("Admin");

        // Customer may ONLY view their own appointments; Admin may view any or all
        int targetUserId = isAdmin ? (userId ?? 0) : currentId;
        if (targetUserId == 0)
        {
            if (isAdmin)
            {
                var all = await _appointmentService.GetAllAppointmentsAsync();
                return Ok(new { success = true, appointments = all });
            }
            return BadRequest(new { message = "User ID is required." });
        }

        var list = await _appointmentService.GetUserAppointmentsAsync(targetUserId);
        return Ok(new { success = true, appointments = list });
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAppointmentStatusRequest request)
    {
        var isAdmin = User.IsInRole("Admin");
        var doctorId = GetCurrentPrincipalId();

        try
        {
            var success = await _appointmentService.UpdateStatusAsync(id, request.Status, doctorId, isAdmin);
            if (!success) return NotFound(new { message = "Appointment not found." });
            return Ok(new { message = "Appointment status updated successfully." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
