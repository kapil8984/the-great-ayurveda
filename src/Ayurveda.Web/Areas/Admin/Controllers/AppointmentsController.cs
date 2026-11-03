using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookieScheme", Policy = "RequireCustomerSupport")]
public class AppointmentsController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;

    public AppointmentsController(AyurvedaDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // -------------------------------------------------------------------------
    // 1. ALL APPOINTMENTS DIRECTORY
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Index(string? status, string? search, int page = 1)
    {
        int pageSize = 15;
        var q = _context.Appointments
            .Include(a => a.Doctor)
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            q = q.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(a => a.PatientName.Contains(search) || a.Doctor.DoctorName.Contains(search) || (a.HealthConcern != null && a.HealthConcern.Contains(search)));
        }

        var total = await q.CountAsync();
        var list = await q
            .OrderByDescending(a => a.StartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SelectedStatus = status ?? "All";
        ViewBag.SearchQuery = search;
        ViewBag.TotalCount = total;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

        return View(list);
    }

    // -------------------------------------------------------------------------
    // 2. APPOINTMENT DETAILS & DIGITAL RX
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Details(int id)
    {
        var appt = await _context.Appointments
            .Include(a => a.Doctor)
                .ThenInclude(d => d.DoctorDetail)
            .Include(a => a.User)
            .Include(a => a.Review)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appt == null) return NotFound();

        ViewBag.ActiveDoctors = await _context.Doctors
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1)
            .OrderBy(d => d.DoctorName)
            .ToListAsync();

        return View(appt);
    }

    // -------------------------------------------------------------------------
    // 3. CANCEL & REASSIGN ACTIONS
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var appt = await _context.Appointments.FindAsync(id);
        if (appt != null)
        {
            appt.Status = "Cancelled";
            appt.PrescriptionNotes = $"Cancelled by Administrator: {reason ?? "Administrative Cancellation"}";
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("AppointmentCancelled", "Appointment", id.ToString(), $"Cancelled appointment #{id}. Reason: {reason ?? "Admin request"}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"Appointment #{id} has been cancelled.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public IActionResult Cancel(int id) => RedirectToAction(nameof(Details), new { id });

    [HttpGet]
    public IActionResult Reassign(int id) => RedirectToAction(nameof(Details), new { id });

    [HttpGet]
    public IActionResult MarkNoShow(int id) => RedirectToAction(nameof(Details), new { id });

    [HttpGet]
    public IActionResult ResolveDispute(int id) => RedirectToAction(nameof(Details), new { id });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reassign(int id, int newDoctorId)
    {
        var appt = await _context.Appointments.FindAsync(id);
        var doc = await _context.Doctors.FindAsync(newDoctorId);
        if (appt != null && doc != null)
        {
            var oldDocId = appt.DoctorId;
            appt.DoctorId = doc.Id;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("AppointmentReassigned", "Appointment", id.ToString(), $"Reassigned appointment #{id} from doctor #{oldDocId} to Dr. {doc.DoctorName}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"Appointment #{id} reassigned to Dr. {doc.DoctorName}.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------------------------------------
    // 4. NO-SHOW HANDLING & DISPUTE RESOLUTION
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNoShow(int id, string party)
    {
        var appt = await _context.Appointments.FindAsync(id);
        if (appt == null) return NotFound();

        appt.Status = "Cancelled";
        appt.PrescriptionNotes = $"Marked as {party} No-Show by Admin.";
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("AppointmentNoShow", "Appointment", id.ToString(), $"Flagged appointment #{id} as {party} No-Show", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Appointment #{id} recorded as {party} No-Show.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveDispute(int id, string resolutionNotes, bool issueRefund)
    {
        var appt = await _context.Appointments.FindAsync(id);
        if (appt == null) return NotFound();

        if (issueRefund)
        {
            appt.Status = "Cancelled";
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("AppointmentDisputeResolved", "Appointment", id.ToString(), $"Dispute resolved on #{id}. Refund Issued: {issueRefund}. Notes: {resolutionNotes}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Dispute resolved for Appointment #{id}. Refund flag: {issueRefund}.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
