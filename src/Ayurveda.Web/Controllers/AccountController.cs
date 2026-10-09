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
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var isDoctor = Request.Path.Value?.Contains("doctor") == true;
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

        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid email address or password.");
            return View(model);
        }

        // Lockout check with max 5 attempts
        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (await _userManager.IsInRoleAsync(user, "Doctor"))
            {
                var doc = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id || d.Email == user.Email);
                if (doc?.DoctorDetail == null || doc.DoctorDetail.Status == 0 || doc.DoctorDetail.Status == 2)
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

        otpRecord.IsUsed = true;
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

        // Upload documents securely via IFileStorageService
        string degreeDocUrl = "/uploads/documents/default-degree.pdf";
        if (model.DegreeCertificate != null)
        {
            using var stream = model.DegreeCertificate.OpenReadStream();
            degreeDocUrl = await _fileStorage.SaveFileAsync(stream, model.DegreeCertificate.FileName, "documents");
        }

        var defaultCategory = await _context.Categories.FirstOrDefaultAsync();
        var specializationId = model.SpecializationId > 0 ? model.SpecializationId : (defaultCategory?.Id ?? 1);

        var doctor = new Doctor
        {
            ApplicationUserId = user.Id,
            DoctorName = model.FullName.Trim(),
            Email = normalizedEmail,
            ContactNumber = model.ContactNumber.Trim(),
            CreatedAt = DateTime.UtcNow,
            DoctorDetail = new DoctorDetail
            {
                Qualification = model.Qualification.Trim(),
                QualificationImage = degreeDocUrl,
                RegistrationNumber = model.RegistrationNumber.Trim(),
                RegistrationCouncil = model.RegistrationCouncil.Trim(),
                SpecializationId = specializationId,
                Experience = model.ExperienceYears,
                Language = model.Languages.Trim(),
                ConsultationFee = model.ConsultationFee,
                ClinicAddress = model.ClinicAddress ?? string.Empty,
                Bio = model.Bio,
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

        otpRecord.IsUsed = true;
        await _context.SaveChangesAsync();

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
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
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
                City = a.City,
                State = a.State,
                PostalCode = a.PostalCode,
                IsDefault = a.IsDefault
            })
            .ToListAsync();

        var totalOrders = await _context.Orders.CountAsync(o => o.UserId == user.Id);
        var totalAppts = await _context.Appointments.CountAsync(a => a.UserId == user.Id);

        var model = new UserProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            ContactNumber = user.ContactNumber,
            Addresses = addresses,
            TotalOrders = totalOrders,
            TotalAppointments = totalAppts
        };

        return View(model);
    }

    // --------------------------------------------------------------------------
    // 7. SECURE POST LOGOUT
    // --------------------------------------------------------------------------

    [HttpPost]
    [Route("account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        TempData["SuccessMessage"] = "You have been signed out safely.";
        return RedirectToAction("Index", "Home");
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
