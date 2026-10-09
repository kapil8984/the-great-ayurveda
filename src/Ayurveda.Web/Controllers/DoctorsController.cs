using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class DoctorsController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICallProvider _callProvider;

    public DoctorsController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager, ICallProvider callProvider)
    {
        _context = context;
        _userManager = userManager;
        _callProvider = callProvider;
    }

    public async Task<IActionResult> Index()
    {
        // Only display approved doctors
        var doctors = await _context.Doctors
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1)
            .Select(d => new DoctorCardViewModel
            {
                Id = d.Id,
                DoctorName = d.DoctorName,
                SpecializationName = d.DoctorDetail!.Specialization.Name,
                ExperienceYears = d.DoctorDetail.Experience,
                Qualification = d.DoctorDetail.Qualification,
                Languages = d.DoctorDetail.Language,
                ConsultationFee = d.DoctorDetail.ConsultationFee,
                Rating = d.DoctorDetail.Rating,
                ReviewCount = d.DoctorDetail.ReviewCount,
                IsOnline = d.DoctorDetail.IsOnline,
                ProfileImage = d.ProfileImage
            })
            .ToListAsync();

        return View(doctors);
    }

    public async Task<IActionResult> Profile(int id)
    {
        var doc = await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .FirstOrDefaultAsync(d => d.Id == id && d.DoctorDetail != null && d.DoctorDetail.Status == 1);

        if (doc == null || doc.DoctorDetail == null)
            return NotFound();

        var model = new DoctorDetailsViewModel
        {
            Id = doc.Id,
            DoctorName = doc.DoctorName,
            SpecializationName = doc.DoctorDetail.Specialization.Name,
            ExperienceYears = doc.DoctorDetail.Experience,
            Qualification = doc.DoctorDetail.Qualification,
            RegistrationNumber = doc.DoctorDetail.RegistrationNumber,
            RegistrationCouncil = doc.DoctorDetail.RegistrationCouncil,
            Languages = doc.DoctorDetail.Language,
            ConsultationFee = doc.DoctorDetail.ConsultationFee,
            Bio = doc.DoctorDetail.Bio,
            ClinicAddress = doc.DoctorDetail.ClinicAddress,
            ProfileImage = doc.ProfileImage,
            Rating = doc.DoctorDetail.Rating,
            ReviewCount = doc.DoctorDetail.ReviewCount,
            IsOnline = doc.DoctorDetail.IsOnline
        };

        return View(model);
    }

    public async Task<IActionResult> Book(int id)
    {
        var doc = await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .FirstOrDefaultAsync(d => d.Id == id && d.DoctorDetail != null && d.DoctorDetail.Status == 1);

        if (doc == null || doc.DoctorDetail == null)
            return NotFound();

        var model = new BookAppointmentViewModel
        {
            DoctorId = doc.Id,
            DoctorName = doc.DoctorName,
            SpecializationName = doc.DoctorDetail.Specialization.Name,
            ConsultationFee = doc.DoctorDetail.ConsultationFee
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookPost(BookAppointmentViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            var demoUser = await _context.Users.FirstOrDefaultAsync();
            if (demoUser == null)
            {
                demoUser = new ApplicationUser { UserName = "guest@ayurveda.local", Email = "guest@ayurveda.local", FullName = "Patient" };
                await _userManager.CreateAsync(demoUser, "GuestPass@2026");
            }
            user = demoUser;
        }

        var doc = await _context.Doctors
            .Include(d => d.DoctorDetail)
            .FirstOrDefaultAsync(d => d.Id == model.DoctorId && d.DoctorDetail != null && d.DoctorDetail.Status == 1);

        if (doc == null)
        {
            TempData["ErrorMessage"] = "Doctor is currently unavailable.";
            return RedirectToAction(nameof(Index));
        }

        // Calculate appointment StartUtc
        var startUtc = DateTime.SpecifyKind(model.SelectedDate.Date.AddHours(10), DateTimeKind.Utc);

        var room = await _callProvider.CreateRoomAsync(doc.Id, doc.DoctorName, model.PatientName);

        var appointment = new Appointment
        {
            DoctorId = doc.Id,
            UserId = user.Id,
            AppointmentDate = model.SelectedDate,
            StartUtc = startUtc,
            TimeSlot = model.SelectedTimeSlot,
            PatientName = model.PatientName,
            PatientContact = model.PatientContact,
            PatientAge = model.PatientAge,
            PatientGender = model.PatientGender,
            HealthConcern = model.HealthConcern,
            ConsultationType = model.ConsultationType,
            ConsultationFee = doc.DoctorDetail!.ConsultationFee,
            Status = "Confirmed",
            MeetingRoomId = room.RoomUrl
        };

        try
        {
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Appointment successfully scheduled! You can join the room at the appointed time.";
            return RedirectToAction(nameof(MyAppointments));
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "This doctor slot was just booked by another patient. Please choose an alternate slot.";
            return RedirectToAction(nameof(Book), new { id = model.DoctorId });
        }
    }

    public async Task<IActionResult> MyAppointments()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            var demoUser = await _context.Users.FirstOrDefaultAsync();
            if (demoUser != null) user = demoUser;
        }

        var appointments = await _context.Appointments
            .Include(a => a.Doctor)
                .ThenInclude(d => d.DoctorDetail)
                    .ThenInclude(dd => dd!.Specialization)
            .Where(a => user != null && a.UserId == user.Id)
            .OrderByDescending(a => a.StartUtc)
            .Select(a => new AppointmentDetailsViewModel
            {
                Id = a.Id,
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor.DoctorName,
                SpecializationName = a.Doctor.DoctorDetail != null ? a.Doctor.DoctorDetail.Specialization.Name : "General Ayurveda",
                StartUtc = a.StartUtc,
                TimeSlot = a.TimeSlot,
                ConsultationType = a.ConsultationType,
                ConsultationFee = a.ConsultationFee,
                Status = a.Status,
                PatientName = a.PatientName,
                HealthConcern = a.HealthConcern,
                PrescriptionNotes = a.PrescriptionNotes,
                MeetingRoomUrl = a.MeetingRoomId
            })
            .ToListAsync();

        return View(appointments);
    }
}
