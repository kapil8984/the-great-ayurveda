namespace Ayurveda.Application.ViewModels;

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
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
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Qualification { get; set; } = "BAMS";
    public string RegistrationNumber { get; set; } = string.Empty;
    public string RegistrationCouncil { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public int ExperienceYears { get; set; }
    public string Languages { get; set; } = "English, Hindi";
    public decimal ConsultationFee { get; set; } = 499.00m;
    public string? ClinicAddress { get; set; }
    public string? Bio { get; set; }
}

public class UserProfileViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public string? Gender { get; set; }
    public int TotalOrders { get; set; }
    public int TotalAppointments { get; set; }
    public List<AddressViewModel> Addresses { get; set; } = new();
}
