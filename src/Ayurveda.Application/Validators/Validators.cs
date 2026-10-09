using FluentValidation;
using Ayurveda.Application.ViewModels;

namespace Ayurveda.Application.Validators;

public class LoginViewModelValidator : AbstractValidator<LoginViewModel>
{
    public LoginViewModelValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class RegisterViewModelValidator : AbstractValidator<RegisterViewModel>
{
    public RegisterViewModelValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .MaximumLength(100).WithMessage("Full Name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.ContactNumber)
            .NotEmpty().WithMessage("Contact number is required.")
            .Matches(@"^[0-9+\-\s]{10,15}$").WithMessage("Valid 10-digit mobile number required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.AgreeToTerms)
            .Equal(true).WithMessage("You must agree to the terms and privacy policy.");
    }
}

public class ForgotPasswordViewModelValidator : AbstractValidator<ForgotPasswordViewModel>
{
    public ForgotPasswordViewModelValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}

public class ResetPasswordOtpViewModelValidator : AbstractValidator<ResetPasswordOtpViewModel>
{
    public ResetPasswordOtpViewModelValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("6-digit OTP is required.")
            .Length(6).WithMessage("OTP must be 6 digits.")
            .Matches(@"^[0-9]{6}$").WithMessage("OTP must be numeric.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public class AddressViewModelValidator : AbstractValidator<AddressViewModel>
{
    public AddressViewModelValidator()
    {
        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("Full name is required.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^[0-9+\-\s]{10,15}$").WithMessage("Enter a valid phone number.");

        RuleFor(x => x.AddressLine1)
            .NotEmpty().WithMessage("House/Flat/Building and Street address is required.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required.");

        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("PIN Code is required.")
            .Matches(@"^[0-9]{6}$").WithMessage("Valid 6-digit Indian PIN code required.");
    }
}

public class BookAppointmentViewModelValidator : AbstractValidator<BookAppointmentViewModel>
{
    public BookAppointmentViewModelValidator()
    {
        RuleFor(x => x.DoctorId)
            .GreaterThan(0).WithMessage("Doctor is required.");

        RuleFor(x => x.SelectedDate)
            .GreaterThanOrEqualTo(DateTime.Today).WithMessage("Consultation date must be today or in the future.");

        RuleFor(x => x.SelectedTimeSlot)
            .NotEmpty().WithMessage("Please choose a time slot.");

        RuleFor(x => x.PatientName)
            .NotEmpty().WithMessage("Patient name is required.");

        RuleFor(x => x.PatientContact)
            .NotEmpty().WithMessage("Patient contact number is required.")
            .Matches(@"^[0-9+\-\s]{10,15}$").WithMessage("Valid phone number required.");
    }
}
