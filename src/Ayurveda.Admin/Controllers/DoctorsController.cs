using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Policy = "RequireCustomerSupport")]
public class DoctorsController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _auditService;
    private readonly IFileStorageService _fileStorage;

    public DoctorsController(AyurvedaDbContext context, IEmailSender emailSender, IAuditService auditService, IFileStorageService fileStorage)
    {
        _context = context;
        _emailSender = emailSender;
        _auditService = auditService;
        _fileStorage = fileStorage;
    }

    // -------------------------------------------------------------------------
    // 0. CREATE DOCTOR
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Create()
    {
        ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string doctorName, string email, string contactNumber, int specializationId, string? qualification, int experience, decimal consultationFee, string? clinicAddress, string? bio, IFormFile? profileImageFile)
    {
        if (string.IsNullOrWhiteSpace(doctorName) || string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Doctor name and email are required.";
            ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            return View();
        }

        string profileImageUrl = "";
        if (profileImageFile != null && profileImageFile.Length > 0)
        {
            using var stream = profileImageFile.OpenReadStream();
            profileImageUrl = await _fileStorage.SaveFileAsync(stream, profileImageFile.FileName, profileImageFile.ContentType);
        }
        else
        {
            profileImageUrl = "/images/doctor-avatar.svg";
        }

        var doctor = new Doctor
        {
            DoctorName = doctorName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            ContactNumber = string.IsNullOrWhiteSpace(contactNumber) ? "+91 98765 00000" : contactNumber.Trim(),
            ProfileImage = profileImageUrl,
            Role = "Doctor",
            CreatedAt = DateTime.UtcNow
        };
        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync();

        var detail = new DoctorDetail
        {
            DoctorId = doctor.Id,
            SpecializationId = specializationId,
            Qualification = string.IsNullOrWhiteSpace(qualification) ? "BAMS" : qualification.Trim(),
            Experience = experience,
            ConsultationFee = consultationFee > 0 ? consultationFee : 499m,
            ClinicAddress = string.IsNullOrWhiteSpace(clinicAddress) ? "Ayurveda Consultation Chamber" : clinicAddress.Trim(),
            Bio = string.IsNullOrWhiteSpace(bio) ? "Experienced Ayurvedic clinician." : bio.Trim(),
            QualificationImage = profileImageUrl,
            Status = 1, // Approved directly by Admin
            IsOnline = true,
            Rating = 5.0,
            ReviewCount = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.DoctorDetails.Add(detail);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("DoctorCreated", "Doctor", doctor.Id.ToString(), $"Admin created doctor Dr. {doctor.DoctorName}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Dr. {doctor.DoctorName} created and approved.";
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 1. ALL DOCTORS DIRECTORY & MANAGEMENT
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Index(string? status, string? specialization, string? search)
    {
        var q = _context.Doctors
            .Where(d => !d.IsDeleted)
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
            doctor.DoctorDetail.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Periodic compliance review and document re-verification required." : reason.Trim();
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

    // -------------------------------------------------------------------------
    // 3B. EDIT DOCTOR PROFILE
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Edit(int id)
    {
        var doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor == null) return NotFound();

        ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

        var model = new AdminDoctorEditViewModel
        {
            Id = doctor.Id,
            DoctorName = doctor.DoctorName,
            Email = doctor.Email,
            ContactNumber = doctor.ContactNumber,
            SpecializationId = doctor.DoctorDetail?.SpecializationId ?? 1,
            Qualification = doctor.DoctorDetail?.Qualification ?? "BAMS",
            Experience = doctor.DoctorDetail?.Experience ?? 5,
            ConsultationFee = doctor.DoctorDetail?.ConsultationFee ?? 499m,
            ClinicAddress = doctor.DoctorDetail?.ClinicAddress,
            Bio = doctor.DoctorDetail?.Bio,
            ProfileImage = doctor.ProfileImage,
            IsOnline = doctor.DoctorDetail?.IsOnline ?? true,
            Status = doctor.DoctorDetail?.Status ?? 1
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminDoctorEditViewModel model, IFormFile? profileImageFile)
    {
        var doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor == null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.DoctorName) || string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(string.Empty, "Doctor name and email are required.");
            ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        doctor.DoctorName = model.DoctorName.Trim();
        doctor.Email = model.Email.Trim().ToLowerInvariant();
        doctor.ContactNumber = model.ContactNumber.Trim();

        if (profileImageFile != null && profileImageFile.Length > 0)
        {
            using var stream = profileImageFile.OpenReadStream();
            doctor.ProfileImage = await _fileStorage.SaveFileAsync(stream, profileImageFile.FileName, profileImageFile.ContentType);
        }

        if (doctor.DoctorDetail != null)
        {
            doctor.DoctorDetail.SpecializationId = model.SpecializationId;
            doctor.DoctorDetail.Qualification = model.Qualification.Trim();
            doctor.DoctorDetail.Experience = model.Experience;
            doctor.DoctorDetail.ConsultationFee = model.ConsultationFee > 0 ? model.ConsultationFee : 499m;
            doctor.DoctorDetail.ClinicAddress = model.ClinicAddress?.Trim() ?? string.Empty;
            doctor.DoctorDetail.Bio = model.Bio?.Trim() ?? string.Empty;
            doctor.DoctorDetail.IsOnline = model.IsOnline;
            doctor.DoctorDetail.Status = model.Status;
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("DoctorUpdated", "Doctor", id.ToString(), $"Updated profile for Dr. {doctor.DoctorName}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["SuccessMessage"] = $"Profile for Dr. {doctor.DoctorName} updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------------------------------------
    // 3C. SAFE DELETE DOCTOR (SOFT DELETE IF REFERENCED, HARD DELETE IF CLEAN)
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor != null)
        {
            var name = doctor.DoctorName;
            var hasAppointments = await _context.Appointments.AnyAsync(a => a.DoctorId == id);
            var hasReviews = await _context.DoctorReviews.AnyAsync(r => r.DoctorId == id);

            if (hasAppointments || hasReviews)
            {
                // Soft delete
                doctor.IsDeleted = true;
                if (doctor.DoctorDetail != null)
                {
                    doctor.DoctorDetail.Status = -1; // Inactive
                    doctor.DoctorDetail.IsOnline = false;
                }
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("DoctorSoftDeleted", "Doctor", id.ToString(), $"Soft-deleted Dr. {name} (referenced by appointments/reviews)", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
                TempData["SuccessMessage"] = $"Dr. {name} has active appointment/review records and was safely soft-deleted (hidden from public directory, records kept).";
            }
            else
            {
                // Hard delete
                if (doctor.DoctorDetail != null)
                {
                    _context.DoctorDetails.Remove(doctor.DoctorDetail);
                }
                _context.Doctors.Remove(doctor);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("DoctorHardDeleted", "Doctor", id.ToString(), $"Permanently deleted Dr. {name}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
                TempData["SuccessMessage"] = $"Dr. {name} had no references and was permanently deleted.";
            }
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
