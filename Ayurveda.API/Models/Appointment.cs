namespace Ayurveda.API.Models;

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
    public string? HealthConcern { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Completed, Cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Doctor Doctor { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
