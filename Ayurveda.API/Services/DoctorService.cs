using System.Text.Json;
using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface IDoctorService
{
    Task<DoctorDto?> GetDoctorByIdAsync(int id);
    Task<DoctorDetailDto?> GetDoctorDetailsAsync(int doctorId);
    Task<List<DoctorDto>> GetAllDoctorsAsync();
    Task<List<DoctorDto>> GetActiveDoctorsAsync();
    Task<List<PendingDoctorInfoDto>> GetPendingDoctorsAsync();
    Task<DoctorDetailDto> AddOrUpdateDoctorDetailsAsync(int doctorId, AddDoctorDetailsRequest request);
    Task<bool> ApproveDoctorAsync(int doctorId);
    Task<bool> RejectDoctorAsync(int doctorId);
    Task<DoctorDto?> UpdateDoctorProfileAsync(int doctorId, string? doctorName, string? contactNumber, IFormFile? profileImage);
    Task<bool> RemoveDoctorAsync(int doctorId);
}

public class DoctorService : IDoctorService
{
    private readonly AyurvedaDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public DoctorService(AyurvedaDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<DoctorDto?> GetDoctorByIdAsync(int id)
    {
        var doctor = await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .FirstOrDefaultAsync(d => d.Id == id);

        return doctor == null ? null : MapToDto(doctor);
    }

    public async Task<DoctorDetailDto?> GetDoctorDetailsAsync(int doctorId)
    {
        var detail = await _context.DoctorDetails
            .Include(dd => dd.Specialization)
            .FirstOrDefaultAsync(dd => dd.DoctorId == doctorId);

        return detail == null ? null : MapDetailToDto(detail);
    }

    public async Task<List<DoctorDto>> GetAllDoctorsAsync()
    {
        var doctors = await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1)
            .OrderByDescending(d => d.Id)
            .ToListAsync();

        return doctors.Select(MapToDto).ToList();
    }

    public async Task<List<DoctorDto>> GetActiveDoctorsAsync()
    {
        // Notice fix from old backend: status is on DoctorDetail, not Doctor!
        var doctors = await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1)
            .OrderByDescending(d => d.DoctorDetail!.Experience)
            .ToListAsync();

        return doctors.Select(MapToDto).ToList();
    }

    public async Task<List<PendingDoctorInfoDto>> GetPendingDoctorsAsync()
    {
        var pendingDetails = await _context.DoctorDetails
            .Include(dd => dd.Specialization)
            .Include(dd => dd.Doctor)
            .Where(dd => dd.Status == 0)
            .OrderByDescending(dd => dd.CreatedAt)
            .ToListAsync();

        return pendingDetails.Select(dd => new PendingDoctorInfoDto(
            MapToDto(dd.Doctor),
            MapDetailToDto(dd)
        )).ToList();
    }

    public async Task<DoctorDetailDto> AddOrUpdateDoctorDetailsAsync(int doctorId, AddDoctorDetailsRequest request)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor == null) throw new InvalidOperationException("Doctor not found");

        var existing = await _context.DoctorDetails.FirstOrDefaultAsync(dd => dd.DoctorId == doctorId);

        string qualificationImage = existing?.QualificationImage ?? string.Empty;
        if (request.QualificationImage != null)
        {
            qualificationImage = await _fileStorage.SaveFileAsync(request.QualificationImage);
        }

        var morningTimesJson = JsonSerializer.Serialize(request.MorningTimes ?? new List<string>());
        var afternoonTimesJson = JsonSerializer.Serialize(request.AfternoonTimes ?? new List<string>());
        var eveningTimesJson = JsonSerializer.Serialize(request.EveningTimes ?? new List<string>());

        if (existing == null)
        {
            existing = new DoctorDetail
            {
                DoctorId = doctorId,
                QualificationImage = qualificationImage,
                Experience = request.Experience,
                Gender = request.Gender.Trim(),
                Language = request.Language.Trim(),
                ClinicAddress = request.ClinicAddress.Trim(),
                SpecializationId = request.Specialization,
                Status = 0, // Pending review
                MorningTimesJson = morningTimesJson,
                AfternoonTimesJson = afternoonTimesJson,
                EveningTimesJson = eveningTimesJson,
                CreatedAt = DateTime.UtcNow
            };
            _context.DoctorDetails.Add(existing);
        }
        else
        {
            existing.QualificationImage = string.IsNullOrEmpty(qualificationImage) ? existing.QualificationImage : qualificationImage;
            existing.Experience = request.Experience;
            existing.Gender = request.Gender.Trim();
            existing.Language = request.Language.Trim();
            existing.ClinicAddress = request.ClinicAddress.Trim();
            existing.SpecializationId = request.Specialization;
            existing.Status = 0; // Reset to pending if updated
            existing.MorningTimesJson = morningTimesJson;
            existing.AfternoonTimesJson = afternoonTimesJson;
            existing.EveningTimesJson = eveningTimesJson;
        }

        await _context.SaveChangesAsync();
        await _context.Entry(existing).Reference(e => e.Specialization).LoadAsync();

        return MapDetailToDto(existing);
    }

    public async Task<bool> ApproveDoctorAsync(int doctorId)
    {
        var detail = await _context.DoctorDetails.FirstOrDefaultAsync(dd => dd.DoctorId == doctorId);
        if (detail == null) return false;

        detail.Status = 1;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectDoctorAsync(int doctorId)
    {
        var detail = await _context.DoctorDetails.FirstOrDefaultAsync(dd => dd.DoctorId == doctorId);
        if (detail == null) return false;

        detail.Status = -1;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<DoctorDto?> UpdateDoctorProfileAsync(int doctorId, string? doctorName, string? contactNumber, IFormFile? profileImage)
    {
        var doctor = await _context.Doctors.Include(d => d.DoctorDetail).ThenInclude(dd => dd!.Specialization).FirstOrDefaultAsync(d => d.Id == doctorId);
        if (doctor == null) return null;

        if (!string.IsNullOrWhiteSpace(doctorName)) doctor.DoctorName = doctorName.Trim();
        if (!string.IsNullOrWhiteSpace(contactNumber)) doctor.ContactNumber = contactNumber.Trim();

        if (profileImage != null)
        {
            doctor.ProfileImage = await _fileStorage.SaveFileAsync(profileImage);
        }

        await _context.SaveChangesAsync();
        return MapToDto(doctor);
    }

    public async Task<bool> RemoveDoctorAsync(int doctorId)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor == null) return false;

        _context.Doctors.Remove(doctor);
        await _context.SaveChangesAsync();
        return true;
    }

    private static DoctorDto MapToDto(Doctor d) => new(
        d.Id,
        d.DoctorName,
        d.Email,
        d.ContactNumber,
        d.ProfileImage,
        d.Role,
        d.CreatedAt,
        d.DoctorDetail != null ? MapDetailToDto(d.DoctorDetail) : null
    );

    private static DoctorDetailDto MapDetailToDto(DoctorDetail dd) => new(
        dd.Id,
        dd.DoctorId,
        dd.QualificationImage,
        dd.Experience,
        dd.Gender,
        dd.Language,
        dd.ClinicAddress,
        dd.SpecializationId,
        dd.Specialization?.Name,
        dd.Status,
        DeserializeTimes(dd.MorningTimesJson),
        DeserializeTimes(dd.AfternoonTimesJson),
        DeserializeTimes(dd.EveningTimesJson),
        dd.CreatedAt
    );

    private static List<string> DeserializeTimes(string json)
    {
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
