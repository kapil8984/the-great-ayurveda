namespace Ayurveda.API.DTOs;

public record AppointmentDto(
    int Id,
    int DoctorId,
    string DoctorName,
    string? DoctorSpecialization,
    string? DoctorClinicAddress,
    string? DoctorProfileImage,
    int UserId,
    string UserName,
    string UserEmail,
    DateTime AppointmentDate,
    string TimeSlot,
    string PatientName,
    string PatientContact,
    string? HealthConcern,
    string Status,
    DateTime CreatedAt
);

public class BookAppointmentRequest
{
    public int? UserId { get; set; }
    public int DoctorId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientContact { get; set; } = string.Empty;
    public string? HealthConcern { get; set; }
}

public record UpdateAppointmentStatusRequest(string Status);
