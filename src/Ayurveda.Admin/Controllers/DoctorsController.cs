using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Roles = "SuperAdmin,SupportAgent,Admin")]
public class DoctorsController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IEmailSender _emailSender;

    public DoctorsController(AyurvedaDbContext context, IEmailSender emailSender)
    {
        _context = context;
        _emailSender = emailSender;
    }

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
                Specialization = d.DoctorDetail != null ? d.DoctorDetail.Specialization.Name : "General",
                ExperienceYears = d.DoctorDetail != null ? d.DoctorDetail.Experience : 0,
                Qualification = d.DoctorDetail != null ? d.DoctorDetail.Qualification ?? "BAMS" : "BAMS",
                QualificationImage = d.DoctorDetail != null ? d.DoctorDetail.QualificationImage : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return View(pendingDocs);
    }

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

            await _emailSender.SendEmailAsync(
                doctor.Email,
                "The Great Ayurveda - Clinical Application Approved",
                $"<p>Dear Dr. {doctor.DoctorName},</p><p>Your AYUSH credentials have been verified and approved. Your consultation calendar is now active for patients.</p>");

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

            await _emailSender.SendEmailAsync(
                doctor.Email,
                "The Great Ayurveda - Application Review Status",
                $"<p>Dear Dr. {doctor.DoctorName},</p><p>Regarding your application: {rejectionReason}</p>");

            TempData["SuccessMessage"] = $"Dr. {doctor.DoctorName}'s application has been rejected with notice.";
        }

        return RedirectToAction(nameof(Pending));
    }
}
