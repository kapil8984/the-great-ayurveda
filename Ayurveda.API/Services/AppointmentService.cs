using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface IAppointmentService
{
    Task<AppointmentDto> BookAppointmentAsync(int userId, BookAppointmentRequest request);
    Task<List<AppointmentDto>> GetDoctorAppointmentsAsync(int doctorId);
    Task<List<AppointmentDto>> GetUserAppointmentsAsync(int userId);
    Task<List<AppointmentDto>> GetAllAppointmentsAsync();
    Task<bool> UpdateStatusAsync(int appointmentId, string status, int currentDoctorId = 0, bool isAdmin = false);
}

public class AppointmentService : IAppointmentService
{
    private readonly AyurvedaDbContext _context;

    public AppointmentService(AyurvedaDbContext context)
    {
        _context = context;
    }

    public static DateTime CalculateStartUtc(DateTime appointmentDate, string timeSlot)
    {
        var baseDate = DateTime.SpecifyKind(appointmentDate.Date, DateTimeKind.Utc);
        if (DateTime.TryParse(timeSlot.Trim(), out var parsedTime))
        {
            return baseDate.Add(parsedTime.TimeOfDay);
        }
        return baseDate;
    }

    public async Task<AppointmentDto> BookAppointmentAsync(int userId, BookAppointmentRequest request)
    {
        var doctor = await _context.Doctors.FindAsync(request.DoctorId);
        if (doctor == null) throw new InvalidOperationException("Doctor not found");

        var user = await _context.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found");

        var startUtc = CalculateStartUtc(request.AppointmentDate, request.TimeSlot);

        // Validate double-booking prevention on server
        var slotExists = await _context.Appointments
            .AnyAsync(a => a.DoctorId == request.DoctorId
                        && a.StartUtc == startUtc
                        && a.Status != "Cancelled"
                        && a.Status != "Declined");

        if (slotExists)
        {
            throw new InvalidOperationException($"The time slot '{request.TimeSlot.Trim()}' on {request.AppointmentDate:yyyy-MM-dd} is already booked for this doctor. Please choose an alternate slot or date.");
        }

        var appointment = new Appointment
        {
            DoctorId = request.DoctorId,
            UserId = userId,
            AppointmentDate = request.AppointmentDate,
            StartUtc = startUtc,
            TimeSlot = request.TimeSlot.Trim(),
            PatientName = string.IsNullOrWhiteSpace(request.PatientName) ? user.Username : request.PatientName.Trim(),
            PatientContact = string.IsNullOrWhiteSpace(request.PatientContact) ? user.Contact : request.PatientContact.Trim(),
            HealthConcern = request.HealthConcern?.Trim(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Appointments.Add(appointment);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique filtered index on (DoctorId, StartUtc) caught race condition
            throw new InvalidOperationException("This appointment slot has just been booked by another user. Please choose an alternate slot or date.");
        }

        return (await GetAppointmentDtoByIdAsync(appointment.Id))!;
    }

    public async Task<List<AppointmentDto>> GetDoctorAppointmentsAsync(int doctorId)
    {
        var list = await _context.Appointments
            .Include(a => a.Doctor)
                .ThenInclude(d => d.DoctorDetail)
                    .ThenInclude(dd => dd!.Specialization)
            .Include(a => a.User)
            .Where(a => a.DoctorId == doctorId)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<List<AppointmentDto>> GetUserAppointmentsAsync(int userId)
    {
        var list = await _context.Appointments
            .Include(a => a.Doctor)
                .ThenInclude(d => d.DoctorDetail)
                    .ThenInclude(dd => dd!.Specialization)
            .Include(a => a.User)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<List<AppointmentDto>> GetAllAppointmentsAsync()
    {
        var list = await _context.Appointments
            .Include(a => a.Doctor)
                .ThenInclude(d => d.DoctorDetail)
                    .ThenInclude(dd => dd!.Specialization)
            .Include(a => a.User)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateStatusAsync(int appointmentId, string status, int currentDoctorId = 0, bool isAdmin = false)
    {
        var appt = await _context.Appointments.FindAsync(appointmentId);
        if (appt == null) return false;

        // Authorization check: doctor can only update their own appointments
        if (!isAdmin && currentDoctorId > 0 && appt.DoctorId != currentDoctorId)
        {
            throw new UnauthorizedAccessException("You are not authorized to update this appointment.");
        }

        appt.Status = status;
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task<AppointmentDto?> GetAppointmentDtoByIdAsync(int id)
    {
        var a = await _context.Appointments
            .Include(a => a.Doctor)
                .ThenInclude(d => d.DoctorDetail)
                    .ThenInclude(dd => dd!.Specialization)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        return a == null ? null : MapToDto(a);
    }

    private static AppointmentDto MapToDto(Appointment a) => new(
        a.Id,
        a.DoctorId,
        a.Doctor.DoctorName,
        a.Doctor.DoctorDetail?.Specialization?.Name,
        a.Doctor.DoctorDetail?.ClinicAddress,
        a.Doctor.ProfileImage,
        a.UserId,
        a.User.Username,
        a.User.Email,
        a.AppointmentDate,
        a.TimeSlot,
        a.PatientName,
        a.PatientContact,
        a.HealthConcern,
        a.Status,
        a.CreatedAt
    );
}
