using System.Security.Claims;
using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorService _doctorService;
    private readonly IAuthService _authService;
    private readonly IDashboardService _dashboardService;

    public DoctorsController(
        IDoctorService doctorService,
        IAuthService authService,
        IDashboardService dashboardService)
    {
        _doctorService = doctorService;
        _authService = authService;
        _dashboardService = dashboardService;
    }

    private int GetCurrentDoctorId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return (claim != null && int.TryParse(claim.Value, out int id)) ? id : 0;
    }

    // Public endpoint: returns only approved doctors (Rule k)
    [HttpGet("doctor/doctor-list")]
    [HttpGet("api/doctors")]
    public async Task<IActionResult> GetDoctorList()
    {
        var doctors = await _doctorService.GetAllDoctorsAsync();
        return Ok(new { doctor = doctors });
    }

    // Public endpoint: returns active approved doctors
    [HttpGet("doctor/active")]
    [HttpGet("api/doctors/active")]
    public async Task<IActionResult> GetActiveDoctors()
    {
        var doctors = await _doctorService.GetActiveDoctorsAsync();
        return Ok(doctors);
    }

    [HttpGet("api/doctors/{id}")]
    public async Task<IActionResult> GetDoctorById(int id)
    {
        var doctor = await _doctorService.GetDoctorByIdAsync(id);
        if (doctor == null) return NotFound(new { message = "Doctor not found" });

        // If not approved yet, only the doctor themselves or an Admin may view
        var currentId = GetCurrentDoctorId();
        var isAdmin = User.IsInRole("Admin");
        var isSelf = User.IsInRole("Doctor") && currentId == id;

        if (doctor.DoctorDetail?.Status != 1 && !isAdmin && !isSelf)
        {
            return NotFound(new { message = "Doctor not found or pending verification." });
        }

        return Ok(doctor);
    }

    [HttpPost("doctor/add-doctor-details")]
    [HttpPost("api/doctors/details")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> AddDoctorDetails([FromForm] AddDoctorDetailsRequest request)
    {
        // IDOR KILL: Ignore request.DoctorId from client; derive strictly from JWT (Rule c)
        int doctorId = GetCurrentDoctorId();
        if (doctorId == 0)
        {
            return Unauthorized(new { error = "Unauthorized doctor access." });
        }

        var details = await _doctorService.AddOrUpdateDoctorDetailsAsync(doctorId, request);
        return StatusCode(201, new { message = "Doctor details added successfully", doctorDetail = details });
    }

    [HttpGet("api/doctors/{id}/details")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> GetDoctorDetailsGet(int id)
    {
        int currentId = GetCurrentDoctorId();
        var isAdmin = User.IsInRole("Admin");

        // Doctor may only view their own details; Admin may view any
        if (!isAdmin && currentId != id)
        {
            return Forbid();
        }

        var detail = await _doctorService.GetDoctorDetailsAsync(id);
        if (detail == null) return NotFound(new { message = "Doctor details not found" });
        return Ok(detail);
    }

    public class DoctorIdBody
    {
        public int DoctorId { get; set; }
    }

    [HttpPost("doctor/get-doctor-details")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> GetDoctorDetailsPost([FromBody] DoctorIdBody body)
    {
        int currentId = GetCurrentDoctorId();
        var isAdmin = User.IsInRole("Admin");

        int targetId = isAdmin ? body.DoctorId : currentId;
        var detail = await _doctorService.GetDoctorDetailsAsync(targetId);
        if (detail == null)
        {
            return NotFound(new { message = "Doctor details not found" });
        }
        return Ok(new { doctorDetail = detail });
    }

    public class DoctorProfilePictureRequest
    {
        public IFormFile? Profile { get; set; }
    }

    [HttpPost("doctor/add-profile-picture")]
    [HttpPost("api/doctors/profile-picture")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> AddProfilePicture([FromForm] DoctorProfilePictureRequest request)
    {
        int doctorId = GetCurrentDoctorId();
        if (doctorId == 0 || request.Profile == null)
        {
            return BadRequest(new { error = "Valid profile image is required." });
        }

        var updated = await _doctorService.UpdateDoctorProfileAsync(doctorId, null, null, request.Profile);
        if (updated == null)
        {
            return NotFound(new { error = "Doctor not found." });
        }

        return Ok(new { message = "Profile picture uploaded successfully", filePath = updated.ProfileImage });
    }

    public class UpdateDoctorProfileBody
    {
        public string? DoctorName { get; set; }
        public string? ContactNumber { get; set; }
    }

    [HttpPost("doctor/update-profile")]
    [HttpPut("api/doctors/update-profile")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> UpdateDoctorProfile([FromBody] UpdateDoctorProfileBody body)
    {
        int doctorId = GetCurrentDoctorId();
        if (doctorId == 0)
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        var updated = await _doctorService.UpdateDoctorProfileAsync(doctorId, body.DoctorName, body.ContactNumber, null);
        if (updated == null)
        {
            return NotFound(new { message = "Doctor not found." });
        }

        return Ok(new { message = "Doctor profile updated successfully", doctor = updated });
    }

    public class ChangePasswordRequestBody
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    [HttpPost("doctor/change-password")]
    [HttpPost("api/doctors/change-password")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestBody dto)
    {
        int doctorId = GetCurrentDoctorId();
        if (doctorId == 0)
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        var success = await _authService.ChangeDoctorPasswordAsync(doctorId, dto.OldPassword, dto.NewPassword);
        if (!success)
        {
            return BadRequest(new { message = "Old password is incorrect or Doctor not found." });
        }

        return Ok(new { message = "Password Changed successfully." });
    }

    // Rule a: Doctor delete -> Admin only!
    [HttpDelete("doctor/remove-doctor/{id}")]
    [HttpDelete("api/doctors/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveDoctor(int id)
    {
        var success = await _doctorService.RemoveDoctorAsync(id);
        if (!success) return NotFound(new { message = "Doctor not found." });
        return Ok(new { message = "Doctor deleted successfully." });
    }

    [HttpGet("api/doctors/{id}/dashboard")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> GetDoctorDashboard(int id)
    {
        int currentId = GetCurrentDoctorId();
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin && currentId != id)
        {
            return Forbid();
        }

        var metrics = await _dashboardService.GetDoctorMetricsAsync(id);
        if (metrics == null) return NotFound(new { message = "Doctor not found." });
        return Ok(metrics);
    }
}
