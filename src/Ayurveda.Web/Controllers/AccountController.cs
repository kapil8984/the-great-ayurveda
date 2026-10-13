using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AyurvedaDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IFileStorageService _fileStorage;
    private readonly IConfiguration _configuration;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AyurvedaDbContext context,
        IEmailSender emailSender,
        IFileStorageService fileStorage,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _emailSender = emailSender;
        _fileStorage = fileStorage;
        _configuration = configuration;
    }

    // --------------------------------------------------------------------------
    // 1. CUSTOMER & DOCTOR LOGIN
    // --------------------------------------------------------------------------

    [HttpGet]
    [Route("account/login")]
    [Route("doctor/login")]
    public IActionResult Login(string? returnUrl = null, string? role = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var isDoctor = Request.Path.Value?.Contains("doctor") == true || string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase);
        ViewBag.GoogleSignInEnabled = !string.IsNullOrEmpty(_configuration["Authentication:Google:ClientId"]);
        
        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            IsDoctorLogin = isDoctor
        });
    }

    [HttpPost]
    [Route("account/login")]
    [Route("doctor/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewBag.GoogleSignInEnabled = !string.IsNullOrEmpty(_configuration["Authentication:Google:ClientId"]);

        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user == null || !user.IsActive || user.IsDeleted)
        {
            ModelState.AddModelError(string.Empty, "Invalid email address or password or account deactivated.");
            return View(model);
        }

        // Lockout check with max 5 attempts
        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (await _userManager.IsInRoleAsync(user, "Doctor"))
            {
                var doc = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id || d.Email == user.Email);
                if (doc?.DoctorDetail == null || doc.DoctorDetail.Status != 1)
                {
                    return RedirectToAction("PendingApproval", "DoctorPortal");
                }
                return RedirectToAction("Dashboard", "DoctorPortal");
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Security Alert: Account temporarily locked out for 15 minutes due to 5 consecutive failed login attempts. Please reset your password or try again later.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid email address or password. Please verify your credentials.");
        return View(model);
    }

    // --------------------------------------------------------------------------
    // 2. CUSTOMER SIGN UP WITH EMAIL OTP VERIFICATION
    // --------------------------------------------------------------------------

    [HttpGet]
    [Route("account/register")]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [Route("account/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existing != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists. Please sign in instead.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            ContactNumber = model.ContactNumber.Trim(),
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, UserRole.Customer.ToString());

            // Generate 6-digit OTP for email verification
            var otpCode = GenerateSecureOtp();
            var otpHashed = BCrypt.Net.BCrypt.HashPassword(otpCode);

            var otpEntity = new PasswordResetOtp
            {
                Email = normalizedEmail,
                OtpHashed = otpHashed,
                ExpiryUtc = DateTime.UtcNow.AddMinutes(15),
                Attempts = 0,
                IsUsed = false
            };
            _context.PasswordResetOtps.Add(otpEntity);
            await _context.SaveChangesAsync();

            await _emailSender.SendEmailAsync(
                normalizedEmail,
                "Welcome to The Great Ayurveda - Verify Your Email",
                $"<p>Dear {user.FullName},</p><p>Thank you for creating an account with The Great Ayurveda. Your 6-digit verification code is: <strong>{otpCode}</strong></p><p>This code expires in 15 minutes.</p>");

            return RedirectToAction("VerifyOtp", new { email = normalizedEmail });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // --------------------------------------------------------------------------
    // 3. 6-DIGIT EMAIL OTP VERIFICATION
    // --------------------------------------------------------------------------

    [HttpGet]
    [Route("account/verify-otp")]
    public IActionResult VerifyOtp(string email, string? returnUrl = null)
    {
        if (string.IsNullOrEmpty(email))
            return RedirectToAction("Login");

        return View(new VerifyEmailOtpViewModel
        {
            Email = email,
            ReturnUrl = returnUrl,
            ResendCountdownSeconds = 30
        });
    }

    [HttpPost]
    [Route("account/verify-otp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyEmailOtpViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "User account not found.");
            return View(model);
        }

        var otpRecord = await _context.PasswordResetOtps
            .Where(o => o.Email == normalizedEmail && !o.IsUsed && o.ExpiryUtc > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        var isDemo = _configuration.GetValue<bool>("DemoMode", false);

        if (!isDemo)
        {
            if (otpRecord == null || otpRecord.Attempts >= 5)
            {
                ModelState.AddModelError(string.Empty, "Verification code has expired or exceeded maximum attempts. Please request a new code.");
                return View(model);
            }

            otpRecord.Attempts++;

            var isCorrect = BCrypt.Net.BCrypt.Verify(model.Otp.Trim(), otpRecord.OtpHashed);
            if (!isCorrect)
            {
                await _context.SaveChangesAsync();
                ModelState.AddModelError("Otp", "Invalid verification code. Please check and try again.");
                return View(model);
            }
        }

        if (otpRecord != null)
        {
            otpRecord.IsUsed = true;
        }
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
        await _context.SaveChangesAsync();

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["SuccessMessage"] = "Email verified successfully! Welcome to The Great Ayurveda.";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Route("account/resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest req)
    {
        if (string.IsNullOrEmpty(req?.Email))
            return Json(new { success = false, message = "Email required." });

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
            return Json(new { success = true, message = "Code resent if account exists." });

        var otpCode = GenerateSecureOtp();
        var otpHashed = BCrypt.Net.BCrypt.HashPassword(otpCode);

        var otpEntity = new PasswordResetOtp
        {
            Email = normalizedEmail,
            OtpHashed = otpHashed,
            ExpiryUtc = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            IsUsed = false
        };
        _context.PasswordResetOtps.Add(otpEntity);
        await _context.SaveChangesAsync();

        await _emailSender.SendEmailAsync(
            normalizedEmail,
            "The Great Ayurveda - Verification Code",
            $"<p>Your new 6-digit verification code is: <strong>{otpCode}</strong></p><p>Valid for 15 minutes.</p>");

        return Json(new { success = true, message = "A new verification code has been dispatched to your email." });
    }

    public class ResendOtpRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    // --------------------------------------------------------------------------
    // 4. DOCTOR ONBOARDING WIZARD & REGISTRATION (/doctor/register)
    // --------------------------------------------------------------------------

    [HttpGet]
    [Route("doctor/register")]
    public async Task<IActionResult> DoctorRegister()
    {
        ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        return View(new DoctorRegisterViewModel());
    }

    [HttpPost]
    [Route("doctor/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DoctorRegister(DoctorRegisterViewModel model)
    {
        ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).ToListAsync();

        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existing != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        var existingPhone = await _context.Doctors.AnyAsync(d => d.ContactNumber == model.ContactNumber.Trim());
        if (existingPhone)
        {
            ModelState.AddModelError("ContactNumber", "This contact number is already registered.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            ContactNumber = model.ContactNumber.Trim(),
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, UserRole.Doctor.ToString());

        // Upload documents securely via IFileStorageService (optional)
        string? degreeDocUrl = null;
        if (model.DegreeCertificate != null && model.DegreeCertificate.Length > 0)
        {
            using var stream = model.DegreeCertificate.OpenReadStream();
            degreeDocUrl = await _fileStorage.SaveFileAsync(stream, model.DegreeCertificate.FileName, model.DegreeCertificate.ContentType);
        }

        var defaultCategory = await _context.Categories.FirstOrDefaultAsync();
        var specializationId = model.SpecializationId > 0 ? model.SpecializationId : (defaultCategory?.Id ?? 1);

        // Generate unique initials avatar SVG (never shared doctor-1)
        var avatarUrl = await GenerateInitialsAvatarAsync(model.FullName);

        var doctor = new Doctor
        {
            ApplicationUserId = user.Id,
            DoctorName = model.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = user.PasswordHash ?? BCrypt.Net.BCrypt.HashPassword(model.Password),
            ProfileImage = avatarUrl,
            ContactNumber = model.ContactNumber.Trim(),
            CreatedAt = DateTime.UtcNow,
            DoctorDetail = new DoctorDetail
            {
                Qualification = model.Qualification.Trim(),
                QualificationImage = degreeDocUrl ?? string.Empty,
                RegistrationNumber = model.RegistrationNumber.Trim(),
                RegistrationCouncil = model.RegistrationCouncil.Trim(),
                SpecializationId = specializationId,
                Experience = model.ExperienceYears,
                Gender = "Not Specified",
                Language = model.Languages.Trim(),
                ConsultationFee = model.ConsultationFee > 0 ? model.ConsultationFee : 499.00m,
                ClinicAddress = model.ClinicAddress ?? string.Empty,
                Bio = model.Bio ?? string.Empty,
                Status = 0, // Pending review
                MorningTimesJson = "[\"09:00 AM\",\"10:00 AM\",\"11:00 AM\"]",
                EveningTimesJson = "[\"04:00 PM\",\"05:00 PM\",\"06:00 PM\"]"
            }
        };

        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync();

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("PendingApproval", "DoctorPortal");
    }

    // --------------------------------------------------------------------------
    // 5. FORGOT & RESET PASSWORD FLOW WITH 6-BOX OTP
    // --------------------------------------------------------------------------

    [HttpGet]
    [Route("account/forgot-password")]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [Route("account/forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user != null)
        {
            var otpCode = GenerateSecureOtp();
            var otpHashed = BCrypt.Net.BCrypt.HashPassword(otpCode);

            var otpEntity = new PasswordResetOtp
            {
                Email = normalizedEmail,
                OtpHashed = otpHashed,
                ExpiryUtc = DateTime.UtcNow.AddMinutes(15),
                Attempts = 0,
                IsUsed = false
            };

            _context.PasswordResetOtps.Add(otpEntity);
            await _context.SaveChangesAsync();

            await _emailSender.SendEmailAsync(
                normalizedEmail,
                "The Great Ayurveda - Password Reset Code",
                $"<p>Your one-time password reset code is: <strong>{otpCode}</strong></p><p>This code expires in 15 minutes.</p>");
        }

        TempData["SuccessMessage"] = "If an account exists with this email, a 6-digit verification code has been dispatched.";
        return RedirectToAction("ResetPassword", new { email = normalizedEmail });
    }

    [HttpGet]
    [Route("account/reset-password")]
    public IActionResult ResetPassword(string? email)
    {
        return View(new ResetPasswordOtpViewModel { Email = email ?? string.Empty });
    }

    [HttpPost]
    [Route("account/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordOtpViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Password reset failed. Please check the code or request a new one.");
            return View(model);
        }

        var otpRecord = await _context.PasswordResetOtps
            .Where(o => o.Email == normalizedEmail && !o.IsUsed && o.ExpiryUtc > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        var isDemo = _configuration.GetValue<bool>("DemoMode", false);

        if (!isDemo)
        {
            if (otpRecord == null || otpRecord.Attempts >= 5)
            {
                ModelState.AddModelError(string.Empty, "Verification code has expired or exceeded maximum attempts. Please request a new code.");
                return View(model);
            }

            otpRecord.Attempts++;

            var isCorrect = BCrypt.Net.BCrypt.Verify(model.Otp.Trim(), otpRecord.OtpHashed);
            if (!isCorrect)
            {
                await _context.SaveChangesAsync();
                ModelState.AddModelError("Otp", "Invalid verification code.");
                return View(model);
            }
        }

        if (otpRecord != null)
        {
            otpRecord.IsUsed = true;
            await _context.SaveChangesAsync();
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

        if (resetResult.Succeeded)
        {
            TempData["SuccessMessage"] = "Your password has been reset successfully. Please sign in with your new password.";
            return RedirectToAction("Login");
        }

        foreach (var error in resetResult.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // --------------------------------------------------------------------------
    // 6. USER PROFILE & ADDRESSES
    // --------------------------------------------------------------------------

    [Authorize]
    [HttpGet]
    [Route("account/profile")]
    public async Task<IActionResult> Profile()
    {
        if (User.IsInRole("Doctor"))
        {
            return RedirectToAction("Dashboard", "DoctorPortal");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null || user.IsDeleted)
            return RedirectToAction("Login");

        var addresses = await _context.Addresses
            .Where(a => a.UserId == user.Id)
            .OrderByDescending(a => a.IsDefault)
            .Select(a => new AddressViewModel
            {
                Id = a.Id,
                RecipientName = a.RecipientName,
                Phone = a.Phone,
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2,
                Landmark = a.Landmark,
                City = a.City,
                State = a.State,
                PostalCode = a.PostalCode,
                IsDefault = a.IsDefault,
                AddressType = a.AddressType
            })
            .ToListAsync();

        var totalOrders = await _context.Orders.CountAsync(o => o.UserId == user.Id);
        var totalAppts = await _context.Appointments.CountAsync(a => a.UserId == user.Id);

        var model = new UserProfileViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            ContactNumber = user.ContactNumber,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            ProfileImageUrl = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt,
            Addresses = addresses,
            TotalOrders = totalOrders,
            TotalAppointments = totalAppts
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [Route("account/edit-profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(EditProfileInputModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || user.IsDeleted) return RedirectToAction("Login");

        if (string.IsNullOrWhiteSpace(model.FullName))
        {
            TempData["ErrorMessage"] = "Full Name is required.";
            return RedirectToAction(nameof(Profile));
        }

        user.FullName = model.FullName.Trim();
        user.ContactNumber = model.ContactNumber?.Trim();
        user.DateOfBirth = model.DateOfBirth;
        user.Gender = model.Gender?.Trim();

        if (model.ProfileImage != null && model.ProfileImage.Length > 0)
        {
            try
            {
                using var stream = model.ProfileImage.OpenReadStream();
                user.ProfileImageUrl = await _fileStorage.SaveFileAsync(stream, model.ProfileImage.FileName, model.ProfileImage.ContentType);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error uploading profile image: " + ex.Message;
                return RedirectToAction(nameof(Profile));
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Profile details updated successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost]
    [Route("account/change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInputModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || user.IsDeleted) return RedirectToAction("Login");

        if (string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
        {
            TempData["ErrorMessage"] = "Current password and new password are required.";
            return RedirectToAction(nameof(Profile));
        }

        if (model.NewPassword != model.ConfirmNewPassword)
        {
            TempData["ErrorMessage"] = "New password and confirmation do not match.";
            return RedirectToAction(nameof(Profile));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Password updated successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost]
    [Route("account/add-address")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAddress(AddressViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || user.IsDeleted) return RedirectToAction("Login");

        if (string.IsNullOrWhiteSpace(model.RecipientName) || string.IsNullOrWhiteSpace(model.AddressLine1) ||
            string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.PostalCode))
        {
            TempData["ErrorMessage"] = "Recipient name, address, city, and PIN code are required.";
            return RedirectToAction(nameof(Profile));
        }

        if (model.IsDefault)
        {
            var existingDefaults = await _context.Addresses.Where(a => a.UserId == user.Id && a.IsDefault).ToListAsync();
            foreach (var d in existingDefaults)
            {
                d.IsDefault = false;
            }
        }

        var address = new Address
        {
            UserId = user.Id,
            RecipientName = model.RecipientName.Trim(),
            Phone = model.Phone?.Trim() ?? string.Empty,
            AddressLine1 = model.AddressLine1.Trim(),
            AddressLine2 = model.AddressLine2?.Trim(),
            Landmark = model.Landmark?.Trim(),
            City = model.City.Trim(),
            State = model.State.Trim(),
            PostalCode = model.PostalCode.Trim(),
            IsDefault = model.IsDefault,
            AddressType = model.AddressType,
            CreatedAt = DateTime.UtcNow
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "New delivery address added successfully.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost]
    [Route("account/edit-address")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAddress(AddressViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || user.IsDeleted) return RedirectToAction("Login");

        var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == model.Id && a.UserId == user.Id);
        if (address == null)
        {
            TempData["ErrorMessage"] = "Address not found or unauthorized.";
            return RedirectToAction(nameof(Profile));
        }

        if (string.IsNullOrWhiteSpace(model.RecipientName) || string.IsNullOrWhiteSpace(model.AddressLine1) ||
            string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.PostalCode))
        {
            TempData["ErrorMessage"] = "Recipient name, address, city, and PIN code are required.";
            return RedirectToAction(nameof(Profile));
        }

        if (model.IsDefault && !address.IsDefault)
        {
            var existingDefaults = await _context.Addresses.Where(a => a.UserId == user.Id && a.IsDefault).ToListAsync();
            foreach (var d in existingDefaults)
            {
                d.IsDefault = false;
            }
        }

        address.RecipientName = model.RecipientName.Trim();
        address.Phone = model.Phone?.Trim() ?? string.Empty;
        address.AddressLine1 = model.AddressLine1.Trim();
        address.AddressLine2 = model.AddressLine2?.Trim();
        address.Landmark = model.Landmark?.Trim();
        address.City = model.City.Trim();
        address.State = model.State.Trim();
        address.PostalCode = model.PostalCode.Trim();
        address.IsDefault = model.IsDefault;
        address.AddressType = model.AddressType;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Delivery address updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost]
    [Route("account/set-default-address/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || user.IsDeleted) return RedirectToAction("Login");

        var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);
        if (address == null)
        {
            TempData["ErrorMessage"] = "Address not found.";
            return RedirectToAction(nameof(Profile));
        }

        var allUserAddresses = await _context.Addresses.Where(a => a.UserId == user.Id).ToListAsync();
        foreach (var a in allUserAddresses)
        {
            a.IsDefault = (a.Id == id);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Default delivery address updated.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost]
    [Route("account/delete-account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || user.IsDeleted) return RedirectToAction("Login");

        // Soft delete: flag IsDeleted = true, IsActive = false, prevent future logins, retain FK historical links
        user.IsDeleted = true;
        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        await _signInManager.SignOutAsync();
        HttpContext.Session.Clear();
        Response.Cookies.Delete("tga_cart_id");

        TempData["SuccessMessage"] = "Your account has been deleted successfully. We hope to serve you again.";
        return RedirectToAction("Index", "Home");
    }

    // --------------------------------------------------------------------------
    // 7. SECURE POST LOGOUT & DESTRUCTIVE ACTIONS (PHASE 9)
    // --------------------------------------------------------------------------

    [HttpPost]
    [Route("account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var isDoctor = User.IsInRole("Doctor");
        await _signInManager.SignOutAsync();
        HttpContext.Session.Clear();

        // Clear cart-merge flags and session cookies
        Response.Cookies.Delete("tga_cart_id");

        TempData["SuccessMessage"] = isDoctor 
            ? "Doctor clinical session ended safely." 
            : "You have been logged out successfully.";

        if (isDoctor)
        {
            return RedirectToAction("Login", "Account", new { role = "Doctor" });
        }

        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    [Route("account/logout")]
    public IActionResult LogoutGet()
    {
        // Logout is strictly POST with anti-forgery token. Never allow GET logout links.
        return RedirectToAction("Login", "Account");
    }

    [Authorize]
    [HttpPost]
    [Route("account/delete-address/{id:int}")]
    [Route("account/deleteaddress/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        var addr = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);
        if (addr != null)
        {
            _context.Addresses.Remove(addr);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Delivery address removed successfully.";
        }
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    [Route("account/access-denied")]
    [Route("account/accessdenied")]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task<string> GenerateInitialsAvatarAsync(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string initials = "DR";
        if (parts.Length == 1 && parts[0].Length >= 2)
        {
            initials = parts[0].Substring(0, 2).ToUpper();
        }
        else if (parts.Length >= 2)
        {
            if (parts[0].Equals("Dr", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("Dr.", StringComparison.OrdinalIgnoreCase))
            {
                initials = parts.Length > 2
                    ? $"{parts[1][0]}{parts[2][0]}".ToUpper()
                    : parts[1].Substring(0, Math.Min(2, parts[1].Length)).ToUpper();
            }
            else
            {
                initials = $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            }
        }

        var colors = new[] { "#1F5E3B", "#0C6258", "#936015", "#2C7E50", "#1C4E80", "#6A4C93" };
        var bg = colors[Math.Abs(fullName.GetHashCode()) % colors.Length];
        var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200" width="200" height="200"><rect width="100%" height="100%" rx="30" fill="{bg}"/><circle cx="100" cy="100" r="75" fill="none" stroke="rgba(255,255,255,0.25)" stroke-width="3"/><text x="50%" y="54%" font-family="system-ui, -apple-system, sans-serif" font-size="64" font-weight="700" fill="#FFFFFF" dominant-baseline="middle" text-anchor="middle">{initials}</text></svg>""";

        var bytes = System.Text.Encoding.UTF8.GetBytes(svg);
        using var ms = new MemoryStream(bytes);
        return await _fileStorage.SaveFileAsync(ms, $"avatar_{initials}.svg", "image/svg+xml");
    }

    // Helper: Cryptographically Strong 6-digit OTP
    private static string GenerateSecureOtp()
    {
        var otpBytes = new byte[4];
        RandomNumberGenerator.Fill(otpBytes);
        var otpNumber = (BitConverter.ToUInt32(otpBytes, 0) % 900000) + 100000;
        return otpNumber.ToString();
    }
}
