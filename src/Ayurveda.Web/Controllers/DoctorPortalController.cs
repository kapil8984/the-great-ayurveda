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

    public DoctorPortalController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [AllowAnonymous]
    public IActionResult PendingApproval()
    {
        return View();
    }

    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var doctor = await _context.Doctors
            .Include(d => d.DoctorDetail)
            .FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id || d.Email == user.Email);

        if (doctor?.DoctorDetail?.Status == 0)
        {
            return RedirectToAction(nameof(PendingApproval));
        }

        var appointments = await _context.Appointments
            .Where(a => doctor != null && a.DoctorId == doctor.Id)
            .OrderByDescending(a => a.StartUtc)
            .Select(a => new AppointmentDetailsViewModel
            {
                Id = a.Id,
                DoctorId = a.DoctorId,
                DoctorName = doctor != null ? doctor.DoctorName : "Physician",
                SpecializationName = "Ayurvedic Practice",
                StartUtc = a.StartUtc,
                TimeSlot = a.TimeSlot,
                ConsultationType = a.ConsultationType,
                PatientName = a.PatientName,
                HealthConcern = a.HealthConcern,
                Status = a.Status,
                MeetingRoomUrl = a.MeetingRoomId
            })
            .ToListAsync();

        return View(appointments);
    }
}
