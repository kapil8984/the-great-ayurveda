namespace Ayurveda.Domain.Entities;

public class Doctor
{
    public int Id { get; set; }
    public int? ApplicationUserId { get; set; }
    public virtual ApplicationUser? ApplicationUser { get; set; }
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

public class DoctorDetail
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public virtual Doctor Doctor { get; set; } = null!;

    public string QualificationImage { get; set; } = string.Empty;
    public string? Qualification { get; set; } = "BAMS";
    public string? RegistrationNumber { get; set; }
    public string? RegistrationCouncil { get; set; }
    public int Experience { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ClinicAddress { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public virtual Category Specialization { get; set; } = null!;
    
    // Status: 0 = Pending, 1 = Approved, -1 / 2 = Rejected
    public int Status { get; set; } = 0;
    public string? RejectionReason { get; set; }
    public decimal ConsultationFee { get; set; } = 499.00m;
    public string? Bio { get; set; }
    public bool IsOnline { get; set; } = false;
    public double Rating { get; set; } = 4.8;
    public int ReviewCount { get; set; } = 0;

    // JSON encoded lists of string times
    public string MorningTimesJson { get; set; } = "[]";
    public string AfternoonTimesJson { get; set; } = "[]";
    public string EveningTimesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
