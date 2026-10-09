namespace Ayurveda.API.Models;

public class DoctorDetail
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string QualificationImage { get; set; } = string.Empty;
    public int Experience { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ClinicAddress { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    // Status: 0 = Pending, 1 = Approved, -1 = Rejected
    public int Status { get; set; } = 0;

    // JSON encoded lists of string times
    public string MorningTimesJson { get; set; } = "[]";
    public string AfternoonTimesJson { get; set; } = "[]";
    public string EveningTimesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Doctor Doctor { get; set; } = null!;
    public virtual Category Specialization { get; set; } = null!;
}
