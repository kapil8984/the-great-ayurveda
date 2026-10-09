namespace Ayurveda.API.Models;

public class Doctor
{
    public int Id { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string? ProfileImage { get; set; }
    public string Role { get; set; } = "Doctor";
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual DoctorDetail? DoctorDetail { get; set; }
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
