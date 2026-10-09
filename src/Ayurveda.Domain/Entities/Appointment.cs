using Ayurveda.Domain.Enums;

namespace Ayurveda.Domain.Entities;

public class Appointment
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int UserId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public DateTime StartUtc { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientContact { get; set; } = string.Empty;
    public int? PatientAge { get; set; }
    public string? PatientGender { get; set; }
    public string? HealthConcern { get; set; }
    public string? ReportUrl { get; set; }
    public string? PrescriptionNotes { get; set; }
    public string? MeetingRoomId { get; set; }
    public ConsultationType ConsultationType { get; set; } = ConsultationType.Video;
    public decimal ConsultationFee { get; set; } = 499.00m;
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Completed, Cancelled, Declined
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Doctor Doctor { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}
