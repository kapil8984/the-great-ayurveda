using System.Security.Cryptography;
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

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AyurvedaDbContext context,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _emailSender = emailSender;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // Lockout check
        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (await _userManager.IsInRoleAsync(user, "Doctor"))
            {
                var doc = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id || d.Email == user.Email);
                if (doc?.DoctorDetail?.Status == 0)
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
            ModelState.AddModelError(string.Empty, "Account locked out due to multiple failed login attempts. Please try again after 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
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
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, UserRole.Customer.ToString());
            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["SuccessMessage"] = "Welcome to The Great Ayurveda! Your account has been created.";
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult DoctorRegister()
    {
        return View(new DoctorRegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DoctorRegister(DoctorRegisterViewModel model)
    {
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

        var defaultCategory = await _context.Categories.FirstOrDefaultAsync();

        var doctor = new Doctor
        {
            ApplicationUserId = user.Id,
            DoctorName = model.FullName.Trim(),
            Email = normalizedEmail,
            ContactNumber = model.ContactNumber.Trim(),
            CreatedAt = DateTime.UtcNow,
            DoctorDetail = new DoctorDetail
            {
                Qualification = model.Qualification,
                RegistrationNumber = model.RegistrationNumber,
                RegistrationCouncil = model.RegistrationCouncil,
                SpecializationId = defaultCategory?.Id ?? 1,
                Experience = model.ExperienceYears,
                Language = model.Languages,
                ConsultationFee = model.ConsultationFee,
                Bio = model.Bio,
                Status = 0 // Pending review
            }
        };

        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync();

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("PendingApproval", "DoctorPortal");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user != null)
        {
            // Generate 6-digit cryptographically random OTP
            var otpBytes = new byte[4];
            RandomNumberGenerator.Fill(otpBytes);
            var otpNumber = (BitConverter.ToUInt32(otpBytes, 0) % 900000) + 100000;
            var otpCode = otpNumber.ToString();

            // Hash OTP in DB
            var otpHashed = BCrypt.Net.BCrypt.HashPassword(otpCode);

            var otpEntity = new PasswordResetOtp
            {
                Email = normalizedEmail,
                OtpHashed = otpHashed,
                ExpiryUtc = DateTime.UtcNow.AddMinutes(10),
                Attempts = 0,
                IsUsed = false
            };

            _context.PasswordResetOtps.Add(otpEntity);
            await _context.SaveChangesAsync();

            await _emailSender.SendEmailAsync(
                normalizedEmail,
                "The Great Ayurveda - Password Reset Code",
                $"<p>Your one-time password reset code is: <strong>{otpCode}</strong></p><p>This code expires in 10 minutes.</p>");
        }

        // Generic safe response
        TempData["SuccessMessage"] = "If an account exists with this email, a 6-digit verification code has been sent.";
        return RedirectToAction(nameof(ResetPassword), new { email = normalizedEmail });
    }

    [HttpGet]
    public IActionResult ResetPassword(string email)
    {
        return View(new ResetPasswordOtpViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordOtpViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var otpRecord = await _context.PasswordResetOtps
            .Where(o => o.Email == normalizedEmail && !o.IsUsed && o.ExpiryUtc > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord == null || !BCrypt.Net.BCrypt.Verify(model.Otp, otpRecord.OtpHashed))
        {
            ModelState.AddModelError(string.Empty, "Invalid or expired verification code.");
            return View(model);
        }

        otpRecord.IsUsed = true;
        await _context.SaveChangesAsync();

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        }

        TempData["SuccessMessage"] = "Password has been successfully updated! You may now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction(nameof(Login));

        var addresses = await _context.Addresses
            .Where(a => a.UserId == user.Id)
            .Select(a => new AddressViewModel
            {
                Id = a.Id,
                RecipientName = a.RecipientName,
                Phone = a.Phone,
                AddressLine1 = a.AddressLine1,
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        TempData["SuccessMessage"] = "You have been signed out safely.";
        return RedirectToAction("Index", "Home");
    }
}
