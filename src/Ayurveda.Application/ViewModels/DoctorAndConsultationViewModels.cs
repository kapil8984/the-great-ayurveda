using Ayurveda.Domain.Enums;

namespace Ayurveda.Application.ViewModels;

public static class DoctorNameFormatter
{
    public static string Format(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Ayurvedic Doctor";
        var trimmed = name.Trim();
        while (trimmed.StartsWith("Dr. ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(4).Trim();
        while (trimmed.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(3).Trim();
        return "Dr. " + trimmed;
    }
}

public class DoctorCardViewModel
{
    public int Id { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string FormattedDoctorName => DoctorNameFormatter.Format(DoctorName);
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
    public string FormattedDoctorName => DoctorNameFormatter.Format(DoctorName);
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
    public string FormattedDoctorName => DoctorNameFormatter.Format(DoctorName);
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
    public bool SimulateFailure { get; set; } = false;
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayOrderId { get; set; }
    public bool IsLiveRazorpay { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public string? RazorpaySignature { get; set; }
    public List<string> AvailableSlots { get; set; } = new();
}

public class AppointmentDetailsViewModel
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string FormattedDoctorName => DoctorNameFormatter.Format(DoctorName);
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
    public bool CanCancelOrReschedule { get; set; }
    public bool HasPrescription { get; set; }
    public bool HasReviewed { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewText { get; set; }
}

public class DoctorListViewModel
{
    public List<DoctorCardViewModel> Doctors { get; set; } = new();
    public List<string> Specializations { get; set; } = new();
    public List<string> AvailableLanguages { get; set; } = new();

    // Filters
    public string? SelectedSpecialization { get; set; }
    public string? SelectedExperience { get; set; }
    public string? SelectedLanguage { get; set; }
    public string? SelectedFeeRange { get; set; }
    public string? SelectedGender { get; set; }
    public double? SelectedRating { get; set; }
    public bool OnlineOnly { get; set; }
    public string? SearchQuery { get; set; }
    public string? SortBy { get; set; } = "recommended";
    public int TotalCount => Doctors.Count;
}

public class PrescribedItemDto
{
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty; // e.g. "1 tsp twice daily after meals"
    public string Duration { get; set; } = string.Empty; // e.g. "30 days"
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
}

public class PrescriptionViewModel
{
    public int AppointmentId { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string FormattedDoctorName => DoctorNameFormatter.Format(DoctorName);
    public string Specialization { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? RegistrationCouncil { get; set; }
    public string? ClinicAddress { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int? PatientAge { get; set; }
    public string? PatientGender { get; set; }
    public DateTime ConsultationDate { get; set; }
    public string? DoshaDiagnosis { get; set; }
    public string? ClinicalObservations { get; set; }
    public string? DietaryAdvice { get; set; }
    public List<PrescribedItemDto> PrescribedMedicines { get; set; } = new();
    public DateTime? FollowUpDate { get; set; }
}

public class WritePrescriptionViewModel
{
    public int AppointmentId { get; set; }
    public int DoctorId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? HealthConcern { get; set; }
    public string? ReportUrl { get; set; }
    public string? DoshaDiagnosis { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? DietaryAdvice { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string PrescribedProductsJson { get; set; } = "[]";
}

public class DoctorReviewSubmissionViewModel
{
    public int AppointmentId { get; set; }
    public int DoctorId { get; set; }
    public int Rating { get; set; } = 5;
    public string? ReviewText { get; set; }
}

public class DoctorPortalDashboardViewModel
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string FormattedDoctorName => DoctorNameFormatter.Format(DoctorName);
    public string SpecializationName { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public int TodayAppointmentsCount { get; set; }
    public int TotalCompletedConsultations { get; set; }
    public decimal TotalGrossEarnings { get; set; }
    public decimal NetAvailableBalance { get; set; }
    public List<AppointmentDetailsViewModel> TodaySchedule { get; set; } = new();
    public List<AppointmentDetailsViewModel> UpcomingAppointments { get; set; } = new();
}

public class DoctorAvailabilityViewModel
{
    public int DoctorId { get; set; }
    public bool IsOnline { get; set; }
    public decimal ConsultationFee { get; set; }
    public List<string> MorningSlots { get; set; } = new();
    public List<string> AfternoonSlots { get; set; } = new();
    public List<string> EveningSlots { get; set; } = new();
}

public class DoctorEarningsViewModel
{
    public decimal TotalGross { get; set; }
    public decimal PlatformCommission { get; set; }
    public decimal NetPayable { get; set; }
    public decimal WithdrawnAmount { get; set; }
    public decimal CurrentBalance => NetPayable - WithdrawnAmount;
    public List<AppointmentDetailsViewModel> Consultations { get; set; } = new();
}

public class DoctorPatientHistoryItemViewModel
{
    public int UserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public int ConsultationCount { get; set; }
    public DateTime LastConsultationDate { get; set; }
    public string? LastHealthConcern { get; set; }
    public List<AppointmentDetailsViewModel> Consultations { get; set; } = new();
}

public class CallRoomViewModel
{
    public int AppointmentId { get; set; }
    public string PatientName { get; set; } = "Patient";
    public string DoctorName { get; set; } = "Ayurvedic Doctor";
    public string FormattedDoctorName => DoctorNameFormatter.Format(DoctorName);
    public string DoctorSpecialization { get; set; } = "Ayurveda";
    public string Role { get; set; } = "patient"; // "patient" or "doctor"
    public string UserId { get; set; } = string.Empty;
    public string[] StunUrls { get; set; } = Array.Empty<string>();
    public string? TurnUrl { get; set; }
    public string? TurnUser { get; set; }
    public string? TurnCredential { get; set; }
    public bool IsDemoMode { get; set; } = true;
}

