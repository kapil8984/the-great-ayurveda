namespace Ayurveda.API.DTOs;

public record DoctorDetailDto(
    int Id,
    int DoctorId,
    string QualificationImage,
    int Experience,
    string Gender,
    string Language,
    string ClinicAddress,
    int SpecializationId,
    string? SpecializationName,
    int Status,
    List<string> MorningTimes,
    List<string> AfternoonTimes,
    List<string> EveningTimes,
    DateTime CreatedAt
);

public class AddDoctorDetailsRequest
{
    public int? DoctorId { get; set; }
    public int Experience { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ClinicAddress { get; set; } = string.Empty;
    public int Specialization { get; set; }
    public IFormFile? QualificationImage { get; set; }
    public List<string> MorningTimes { get; set; } = new();
    public List<string> AfternoonTimes { get; set; } = new();
    public List<string> EveningTimes { get; set; } = new();
}

public record DoctorApprovalRequest(int DoctorId);

public record PendingDoctorInfoDto(
    DoctorDto Doctor,
    DoctorDetailDto? DoctorDetails
);
