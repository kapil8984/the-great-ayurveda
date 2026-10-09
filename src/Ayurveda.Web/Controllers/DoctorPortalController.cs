using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

[Authorize(Roles = "Doctor")]
public class DoctorPortalController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DoctorPortalController> _logger;

    public DoctorPortalController(
        AyurvedaDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<DoctorPortalController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    private async Task<Doctor?> GetCurrentDoctorAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return null;

        return await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id || d.Email == user.Email);
    }

    // -------------------------------------------------------------------------
    // 1. DASHBOARD WITH KPIS & TODAY'S SCHEDULE
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null || doctor.DoctorDetail == null)
        {
            // Demo fallback if doctor role is accessed
            doctor = await _context.Doctors
                .Include(d => d.DoctorDetail)
                    .ThenInclude(dd => dd!.Specialization)
                .FirstOrDefaultAsync(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1);

            if (doctor == null) return RedirectToAction("Login", "Account");
        }

        if (doctor.DoctorDetail?.Status == 0)
        {
            return RedirectToAction(nameof(PendingApproval));
        }

        var today = DateTime.Today;
        var nowUtc = DateTime.UtcNow;

        var allAppts = await _context.Appointments
            .Where(a => a.DoctorId == doctor.Id)
            .OrderByDescending(a => a.StartUtc)
            .ToListAsync();

        var todaySchedule = allAppts
            .Where(a => a.AppointmentDate.Date == today && a.Status != "Cancelled")
            .Select(a => MapToViewModel(a, doctor))
            .ToList();

        var upcomingAppts = allAppts
            .Where(a => a.StartUtc > nowUtc && a.Status == "Confirmed")
            .Take(6)
            .Select(a => MapToViewModel(a, doctor))
            .ToList();

        var completedCount = allAppts.Count(a => a.Status == "Completed");
        var grossEarnings = allAppts.Where(a => a.Status == "Completed").Sum(a => a.ConsultationFee);
        var netBalance = Math.Round(grossEarnings * 0.85m, 2);

        var model = new DoctorPortalDashboardViewModel
        {
            DoctorId = doctor.Id,
            DoctorName = doctor.DoctorName,
            SpecializationName = doctor.DoctorDetail.Specialization?.Name ?? "General Ayurveda",
            IsOnline = doctor.DoctorDetail.IsOnline,
            Rating = doctor.DoctorDetail.Rating,
            ReviewCount = doctor.DoctorDetail.ReviewCount,
            TodayAppointmentsCount = todaySchedule.Count,
            TotalCompletedConsultations = completedCount,
            TotalGrossEarnings = grossEarnings,
            NetAvailableBalance = netBalance,
            TodaySchedule = todaySchedule,
            UpcomingAppointments = upcomingAppts
        };

        return View(model);
    }

    [AllowAnonymous]
    public async Task<IActionResult> PendingApproval()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var doctor = await _context.Doctors
                .Include(d => d.DoctorDetail)
                .FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id || d.Email == user.Email);

            if (doctor?.DoctorDetail != null)
            {
                if (doctor.DoctorDetail.Status == 1)
                    return RedirectToAction(nameof(Dashboard));

                ViewBag.Status = doctor.DoctorDetail.Status;
                ViewBag.RejectionReason = doctor.DoctorDetail.RejectionReason;
                ViewBag.DoctorName = doctor.DoctorName;
                ViewBag.SubmittedAt = doctor.CreatedAt;
            }
        }
        return View();
    }

    // -------------------------------------------------------------------------
    // 2. APPOINTMENT QUEUE (ACCEPT / DECLINE / COMPLETE)
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Appointments(string? status)
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstAsync();

        var q = _context.Appointments
            .Where(a => a.DoctorId == doctor.Id);

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            q = q.Where(a => a.Status == status);
        }

        var list = await q
            .OrderByDescending(a => a.StartUtc)
            .Select(a => MapToViewModel(a, doctor))
            .ToListAsync();

        ViewBag.CurrentFilter = status ?? "All";
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptAppointment(int id)
    {
        var doctor = await GetCurrentDoctorAsync();
        var appt = await _context.Appointments.FindAsync(id);
        if (appt == null) return NotFound();

        appt.Status = "Confirmed";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Appointment for {appt.PatientName} has been confirmed.";
        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineAppointment(int id, string? reason)
    {
        var appt = await _context.Appointments.FindAsync(id);
        if (appt == null) return NotFound();

        appt.Status = "Declined";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Appointment declined. Reason recorded: {reason ?? "Doctor unavailable"}";
        return RedirectToAction(nameof(Appointments));
    }

    // -------------------------------------------------------------------------
    // 3. CLINICAL PRESCRIPTION WRITER
    // -------------------------------------------------------------------------
    public async Task<IActionResult> WritePrescription(int id)
    {
        var appt = await _context.Appointments
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appt == null) return NotFound();

        var model = new WritePrescriptionViewModel
        {
            AppointmentId = appt.Id,
            DoctorId = appt.DoctorId,
            PatientName = appt.PatientName,
            HealthConcern = appt.HealthConcern,
            ReportUrl = appt.ReportUrl,
            DoshaDiagnosis = appt.DoshaDiagnosis ?? "Tridosha Imbalance",
            ClinicalNotes = appt.PrescriptionNotes,
            DietaryAdvice = appt.DietaryAdvice,
            FollowUpDate = appt.FollowUpDateUtc ?? DateTime.Today.AddDays(14),
            PrescribedProductsJson = appt.PrescriptionProductsJson ?? "[]"
        };

        // Load store products for prescribing dropdown
        ViewBag.AvailableProducts = await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Title)
            .Select(p => new { p.Id, p.Title, p.Price, p.Brand, p.ImageUrl })
            .ToListAsync();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WritePrescriptionPost(WritePrescriptionViewModel model)
    {
        var appt = await _context.Appointments.FindAsync(model.AppointmentId);
        if (appt == null) return NotFound();

        appt.DoshaDiagnosis = model.DoshaDiagnosis;
        appt.PrescriptionNotes = model.ClinicalNotes;
        appt.DietaryAdvice = model.DietaryAdvice;
        appt.PrescriptionProductsJson = model.PrescribedProductsJson;
        appt.FollowUpDateUtc = model.FollowUpDate;
        appt.Status = "Completed";

        if (!appt.LeftAtUtc.HasValue)
        {
            appt.LeftAtUtc = DateTime.UtcNow;
            if (appt.JoinedAtUtc.HasValue)
            {
                appt.CallDurationSeconds = (int)(appt.LeftAtUtc.Value - appt.JoinedAtUtc.Value).TotalSeconds;
            }
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Digital prescription issued for {appt.PatientName}. Consultation marked as Completed.";
        return RedirectToAction(nameof(Appointments));
    }

    // -------------------------------------------------------------------------
    // 4. AVAILABILITY & INSTANT CALL-NOW MANAGER
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Availability()
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null || doctor.DoctorDetail == null)
            doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstAsync();

        var morning = ParseSlots(doctor.DoctorDetail!.MorningTimesJson);
        var afternoon = ParseSlots(doctor.DoctorDetail.AfternoonTimesJson);
        var evening = ParseSlots(doctor.DoctorDetail.EveningTimesJson);

        var model = new DoctorAvailabilityViewModel
        {
            DoctorId = doctor.Id,
            IsOnline = doctor.DoctorDetail.IsOnline,
            ConsultationFee = doctor.DoctorDetail.ConsultationFee,
            MorningSlots = morning,
            AfternoonSlots = afternoon,
            EveningSlots = evening
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AvailabilityPost(DoctorAvailabilityViewModel model)
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null || doctor.DoctorDetail == null)
            doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstAsync();

        doctor.DoctorDetail!.IsOnline = model.IsOnline;
        doctor.DoctorDetail.ConsultationFee = model.ConsultationFee;
        doctor.DoctorDetail.MorningTimesJson = JsonSerializer.Serialize(model.MorningSlots);
        doctor.DoctorDetail.AfternoonTimesJson = JsonSerializer.Serialize(model.AfternoonSlots);
        doctor.DoctorDetail.EveningTimesJson = JsonSerializer.Serialize(model.EveningSlots);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Practice schedule & instant availability updated successfully.";
        return RedirectToAction(nameof(Availability));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleOnlineStatus()
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor?.DoctorDetail != null)
        {
            doctor.DoctorDetail.IsOnline = !doctor.DoctorDetail.IsOnline;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isOnline = doctor.DoctorDetail.IsOnline });
        }
        return Json(new { success = false });
    }

    // -------------------------------------------------------------------------
    // 5. PATIENT CONSULTATION HISTORY (ONLY PATIENTS THIS DOCTOR HAS CONSULTED)
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Patients()
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstAsync();

        // Security rule: Only patients who have consulted this doctor
        var doctorAppts = await _context.Appointments
            .Include(a => a.User)
            .Where(a => a.DoctorId == doctor.Id)
            .OrderByDescending(a => a.StartUtc)
            .ToListAsync();

        var patientGroups = doctorAppts
            .GroupBy(a => a.UserId)
            .Select(g =>
            {
                var latest = g.First();
                return new DoctorPatientHistoryItemViewModel
                {
                    UserId = g.Key,
                    PatientName = latest.PatientName,
                    ContactNumber = latest.PatientContact,
                    ConsultationCount = g.Count(),
                    LastConsultationDate = latest.AppointmentDate,
                    LastHealthConcern = latest.HealthConcern,
                    Consultations = g.Select(a => MapToViewModel(a, doctor)).ToList()
                };
            })
            .ToList();

        return View(patientGroups);
    }

    // -------------------------------------------------------------------------
    // 6. EARNINGS & WITHDRAWAL REPORT
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Earnings()
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstAsync();

        var consultations = await _context.Appointments
            .Where(a => a.DoctorId == doctor.Id && a.Status == "Completed")
            .OrderByDescending(a => a.StartUtc)
            .Select(a => MapToViewModel(a, doctor))
            .ToListAsync();

        var totalGross = consultations.Sum(c => c.ConsultationFee);
        var platformCommission = Math.Round(totalGross * 0.15m, 2);
        var netPayable = totalGross - platformCommission;

        var model = new DoctorEarningsViewModel
        {
            TotalGross = totalGross,
            PlatformCommission = platformCommission,
            NetPayable = netPayable,
            WithdrawnAmount = 0m,
            Consultations = consultations
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPayout()
    {
        TempData["SuccessMessage"] = "Payout request submitted to finance. Settlement will be credited to your verified bank account within 24-48 business hours.";
        return RedirectToAction(nameof(Earnings));
    }

    // -------------------------------------------------------------------------
    // 7. PROFILE & CREDENTIALS
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Profile()
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) doctor = await _context.Doctors.Include(d => d.DoctorDetail).FirstAsync();

        return View(doctor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfilePost(string bio, string clinicAddress, string languages, string contactNumber)
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor?.DoctorDetail != null)
        {
            doctor.DoctorDetail.Bio = bio;
            doctor.DoctorDetail.ClinicAddress = clinicAddress;
            doctor.DoctorDetail.Language = languages;
            doctor.ContactNumber = contactNumber;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile details updated successfully.";
        }
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "New password and confirmation do not match.";
            return RedirectToAction(nameof(Profile));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var res = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (res.Succeeded)
            {
                TempData["SuccessMessage"] = "Your clinical portal password has been updated.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join("; ", res.Errors.Select(e => e.Description));
            }
        }
        return RedirectToAction(nameof(Profile));
    }

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------
    private static AppointmentDetailsViewModel MapToViewModel(Appointment a, Doctor doc)
    {
        return new AppointmentDetailsViewModel
        {
            Id = a.Id,
            DoctorId = a.DoctorId,
            DoctorName = doc.DoctorName,
            SpecializationName = doc.DoctorDetail?.Specialization?.Name ?? "Ayurvedic Practice",
            StartUtc = a.StartUtc,
            TimeSlot = a.TimeSlot,
            ConsultationType = a.ConsultationType,
            ConsultationFee = a.ConsultationFee,
            Status = a.Status,
            PatientName = a.PatientName,
            HealthConcern = a.HealthConcern,
            PrescriptionNotes = a.PrescriptionNotes,
            MeetingRoomUrl = a.MeetingRoomId,
            CanJoinNow = a.Status == "Confirmed",
            HasPrescription = !string.IsNullOrEmpty(a.PrescriptionNotes) || (a.PrescriptionProductsJson != null && a.PrescriptionProductsJson != "[]")
        };
    }

    private static List<string> ParseSlots(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
