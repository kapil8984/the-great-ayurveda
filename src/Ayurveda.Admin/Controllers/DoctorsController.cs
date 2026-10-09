using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Roles = "SuperAdmin,SupportAgent,Admin")]
public class DoctorsController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _auditService;

    public DoctorsController(AyurvedaDbContext context, IEmailSender emailSender, IAuditService auditService)
    {
        _context = context;
        _emailSender = emailSender;
        _auditService = auditService;
    }

    // -------------------------------------------------------------------------
    // 1. ALL DOCTORS DIRECTORY & MANAGEMENT
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Index(string? status, string? specialization, string? search)
    {
        var q = _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "Approved") q = q.Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1);
            else if (status == "Pending") q = q.Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 0);
            else if (status == "Rejected") q = q.Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == -1);
            else if (status == "Suspended") q = q.Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 2);
        }

        if (!string.IsNullOrWhiteSpace(specialization))
        {
            q = q.Where(d => d.DoctorDetail != null && d.DoctorDetail.Specialization != null && d.DoctorDetail.Specialization.Name == specialization);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(d => d.DoctorName.Contains(search) || d.Email.Contains(search) || (d.ContactNumber != null && d.ContactNumber.Contains(search)));
        }

        var doctorsList = await q.OrderByDescending(d => d.CreatedAt).ToListAsync();

        ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).Select(c => c.Name).OrderBy(n => n).ToListAsync();
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedSpecialization = specialization;
        ViewBag.SearchQuery = search;
        ViewBag.DemoDoctorCount = await _context.DoctorDetails.CountAsync(dd => dd.IsDemoData);

        return View(doctorsList);
    }

    // -------------------------------------------------------------------------
    // 2. PENDING VERIFICATION QUEUE
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Pending()
    {
        var pendingDocs = await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 0)
            .Select(d => new AdminPendingDoctorDto
            {
                DoctorId = d.Id,
                DoctorName = d.DoctorName,
                Email = d.Email,
                ContactNumber = d.ContactNumber,
                Specialization = d.DoctorDetail != null && d.DoctorDetail.Specialization != null ? d.DoctorDetail.Specialization.Name : "General",
                ExperienceYears = d.DoctorDetail != null ? d.DoctorDetail.Experience : 0,
                Qualification = d.DoctorDetail != null ? d.DoctorDetail.Qualification ?? "BAMS" : "BAMS",
                QualificationImage = d.DoctorDetail != null ? d.DoctorDetail.QualificationImage : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return View(pendingDocs);
    }

    // -------------------------------------------------------------------------
    // 3. APPROVE & REJECT ACTIONS
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int doctorId)
    {
        var doctor = await _context.Doctors
            .Include(d => d.DoctorDetail)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor?.DoctorDetail != null)
        {
            doctor.DoctorDetail.Status = 1; // Approved
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("DoctorApproved", "Doctor", doctorId.ToString(), $"Approved credentials for Dr. {doctor.DoctorName}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

            await _emailSender.SendEmailAsync(
                doctor.Email,
                "The Great Ayurveda - Clinical Application Approved",
                $"<p>Dear Dr. {doctor.DoctorName},</p><p>Your AYUSH medical credentials have been verified and approved. Your consultation chamber is now active for patients.</p>");

            TempData["SuccessMessage"] = $"Dr. {doctor.DoctorName} has been approved successfully.";
        }

        return RedirectToAction(nameof(Pending));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int doctorId, string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            TempData["ErrorMessage"] = "A mandatory rejection reason must be provided.";
            return RedirectToAction(nameof(Pending));
        }

        var doctor = await _context.Doctors
            .Include(d => d.DoctorDetail)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor?.DoctorDetail != null)
        {
            doctor.DoctorDetail.Status = -1; // Rejected
            doctor.DoctorDetail.RejectionReason = rejectionReason.Trim();
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("DoctorRejected", "Doctor", doctorId.ToString(), $"Rejected application for Dr. {doctor.DoctorName}. Reason: {rejectionReason}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

            await _emailSender.SendEmailAsync(
                doctor.Email,
                "The Great Ayurveda - Application Review Status",
                $"<p>Dear Dr. {doctor.DoctorName},</p><p>Regarding your practice registration application:</p><blockquote style='color:#777'>{rejectionReason}</blockquote><p>Please reach out to verification@thegreatayurveda.com with updated credentials.</p>");

            TempData["SuccessMessage"] = $"Dr. {doctor.DoctorName}'s application has been rejected with audit notice.";
        }

        return RedirectToAction(nameof(Pending));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestMoreInfo(int doctorId, string instructions)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor != null)
        {
            await _auditService.LogAsync("DoctorInfoRequested", "Doctor", doctorId.ToString(), $"Requested further credentials from Dr. {doctor.DoctorName}: {instructions}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

            await _emailSender.SendEmailAsync(
                doctor.Email,
                "The Great Ayurveda - Additional Information Requested",
                $"<p>Dear Dr. {doctor.DoctorName},</p><p>Our medical vetting committee requests additional documentation:</p><p>{instructions}</p>");

            TempData["SuccessMessage"] = $"Information request dispatched to Dr. {doctor.DoctorName}.";
        }
        return RedirectToAction(nameof(Pending));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int doctorId, string? reason)
    {
        var doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.Id == doctorId);
        if (doctor?.DoctorDetail != null)
        {
            doctor.DoctorDetail.Status = 2; // Suspended
            doctor.DoctorDetail.IsOnline = false;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("DoctorSuspended", "Doctor", doctorId.ToString(), $"Suspended Dr. {doctor.DoctorName}. Reason: {reason ?? "Administrative review"}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"Dr. {doctor.DoctorName} has been suspended from taking consultations.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int doctorId)
    {
        var doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.Id == doctorId);
        if (doctor?.DoctorDetail != null)
        {
            doctor.DoctorDetail.Status = 1; // Approved
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("DoctorReactivated", "Doctor", doctorId.ToString(), $"Reactivated Dr. {doctor.DoctorName}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"Dr. {doctor.DoctorName} has been reactivated.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDetails(int doctorId, decimal fee, int experience, string? clinicAddress, string? bio)
    {
        var doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.Id == doctorId);
        if (doctor?.DoctorDetail != null)
        {
            doctor.DoctorDetail.ConsultationFee = fee;
            doctor.DoctorDetail.Experience = experience;
            if (clinicAddress != null) doctor.DoctorDetail.ClinicAddress = clinicAddress;
            if (bio != null) doctor.DoctorDetail.Bio = bio;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("DoctorUpdated", "Doctor", doctorId.ToString(), $"Updated fee to ₹{fee}, experience to {experience} yrs for Dr. {doctor.DoctorName}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"Credentials and consultation fee updated for Dr. {doctor.DoctorName}.";
        }
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 4. BULK DEMO DATA REMOVAL & BULK APPROVE
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> BulkRemoveDemoDoctors()
    {
        var demoDoctors = await _context.Doctors
            .Include(d => d.DoctorDetail)
            .Include(d => d.Reviews)
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.IsDemoData)
            .ToListAsync();

        var count = demoDoctors.Count;
        foreach (var doc in demoDoctors)
        {
            _context.DoctorReviews.RemoveRange(doc.Reviews);
            if (doc.DoctorDetail != null) _context.DoctorDetails.Remove(doc.DoctorDetail);
            _context.Doctors.Remove(doc);
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("BulkDemoDoctorsRemoved", "Doctor", "Bulk", $"Bulk-purged {count} demo doctor accounts from system", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Successfully bulk-removed {count} demonstration doctor accounts from the platform.";
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 5. DOCTOR 360 DEGREE DETAIL VIEW
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Details(int id)
    {
        var doctor = await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .Include(d => d.Reviews)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doctor == null) return NotFound();

        var recentAppts = await _context.Appointments
            .Where(a => a.DoctorId == id)
            .OrderByDescending(a => a.StartUtc)
            .Take(10)
            .ToListAsync();

        var completed = await _context.Appointments
            .Where(a => a.DoctorId == id && a.Status == "Completed")
            .ToListAsync();

        var gross = completed.Sum(a => a.ConsultationFee);
        var payouts = Math.Round(gross * 0.85m, 2);

        var model = new AdminDoctorDetailsViewModel
        {
            Doctor = doctor,
            RecentAppointments = recentAppts,
            Reviews = doctor.Reviews.OrderByDescending(r => r.CreatedAt).ToList(),
            GrossEarnings = gross,
            SettledPayouts = payouts
        };

        return View(model);
    }
}
