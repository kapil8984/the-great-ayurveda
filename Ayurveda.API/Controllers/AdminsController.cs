using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminsController : ControllerBase
{
    private readonly IDoctorService _doctorService;
    private readonly IDashboardService _dashboardService;

    public AdminsController(IDoctorService doctorService, IDashboardService dashboardService)
    {
        _doctorService = doctorService;
        _dashboardService = dashboardService;
    }

    [HttpGet("admin/get-pending-doctor")]
    [HttpGet("api/admin/pending-doctors")]
    public async Task<IActionResult> GetPendingDoctors()
    {
        var list = await _doctorService.GetPendingDoctorsAsync();
        return Ok(new { pendingDoctorInfo = list });
    }

    [HttpPut("admin/approve-doctor-request-accept")]
    [HttpPut("api/admin/approve-doctor")]
    public async Task<IActionResult> ApproveDoctor([FromBody] DoctorApprovalRequest request)
    {
        var success = await _doctorService.ApproveDoctorAsync(request.DoctorId);
        if (!success)
        {
            return NotFound(new { message = "Doctor details not found" });
        }
        return Ok(new { message = "Doctor request approved successfully" });
    }

    [HttpPut("admin/approve-doctor-request-reject")]
    [HttpPut("api/admin/reject-doctor")]
    public async Task<IActionResult> RejectDoctor([FromBody] DoctorApprovalRequest request)
    {
        var success = await _doctorService.RejectDoctorAsync(request.DoctorId);
        if (!success)
        {
            return NotFound(new { message = "Doctor details not found" });
        }
        return Ok(new { message = "Doctor request rejected successfully" });
    }

    [HttpGet("api/admin/dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var metrics = await _dashboardService.GetAdminMetricsAsync();
        return Ok(metrics);
    }
}
