using Ayurveda.Domain.Enums;

namespace Ayurveda.Application.ViewModels;

public class DoctorCardViewModel
{
    public int Id { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public string? Qualification { get; set; }
    public string? Languages { get; set; }
    public decimal ConsultationFee { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsOnline { get; set; }
    public string? ProfileImage { get; set; }
    public string? ClinicAddress { get; set; }
}

public class DoctorDetailsViewModel
{
    public int Id { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public string? Qualification { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? RegistrationCouncil { get; set; }
    public string? Languages { get; set; }
    public decimal ConsultationFee { get; set; }
    public string? Bio { get; set; }
    public string? ClinicAddress { get; set; }
    public string? ProfileImage { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsOnline { get; set; }
    public List<string> MorningSlots { get; set; } = new();
    public List<string> AfternoonSlots { get; set; } = new();
    public List<string> EveningSlots { get; set; } = new();
}

public class BookAppointmentViewModel
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public DateTime SelectedDate { get; set; } = DateTime.Today.AddDays(1);
    public string SelectedTimeSlot { get; set; } = string.Empty;
    public ConsultationType ConsultationType { get; set; } = ConsultationType.Video;
    public string PatientName { get; set; } = string.Empty;
    public string PatientContact { get; set; } = string.Empty;
    public int? PatientAge { get; set; }
    public string? PatientGender { get; set; }
    public string? HealthConcern { get; set; }
    public List<string> AvailableSlots { get; set; } = new();
}

public class AppointmentDetailsViewModel
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public ConsultationType ConsultationType { get; set; }
    public decimal ConsultationFee { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? HealthConcern { get; set; }
    public string? PrescriptionNotes { get; set; }
    public string? MeetingRoomUrl { get; set; }
    public bool CanJoinNow { get; set; }
}
