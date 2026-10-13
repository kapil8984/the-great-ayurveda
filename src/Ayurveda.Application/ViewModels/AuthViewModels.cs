using Microsoft.AspNetCore.Http;

namespace Ayurveda.Application.ViewModels;

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
    public bool IsDoctorLogin { get; set; }
}

public class RegisterViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public bool AgreeToTerms { get; set; }
}

public class VerifyEmailOtpViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public int ResendCountdownSeconds { get; set; } = 30;
    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordOtpViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class DoctorRegisterViewModel
{
    // Step 1: Account
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    // Step 2: Professional Details
    public string Qualification { get; set; } = "BAMS";
    public string RegistrationNumber { get; set; } = string.Empty;
    public string RegistrationCouncil { get; set; } = "State Ayurvedic Board";
    public int SpecializationId { get; set; }
    public int ExperienceYears { get; set; }
    public string Languages { get; set; } = "English, Hindi";
    public decimal ConsultationFee { get; set; } = 499.00m;
    public string? ClinicAddress { get; set; }
    public string? Bio { get; set; }

    // Step 3: Documents
    public IFormFile? DegreeCertificate { get; set; }
    public IFormFile? CouncilCertificate { get; set; }
    public IFormFile? IdProof { get; set; }

    // Step 4: Availability & Schedule
    public string AvailableDays { get; set; } = "Mon,Tue,Wed,Thu,Fri,Sat";
    public string AvailableTimeSlots { get; set; } = "Morning,Evening";
    public int MaxConsultationsPerDay { get; set; } = 10;
}

public class AdminLoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
    public string? TotpCode { get; set; }
}

public class UserProfileViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalOrders { get; set; }
    public int TotalAppointments { get; set; }
    public List<AddressViewModel> Addresses { get; set; } = new();
}

public class EditProfileInputModel
{
    public string FullName { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public IFormFile? ProfileImage { get; set; }
}

public class ChangePasswordInputModel
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
