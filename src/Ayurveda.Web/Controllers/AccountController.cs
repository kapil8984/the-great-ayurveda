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
    private readonly IOtpService _otpService;
    private readonly ILoginThrottleService _loginThrottle;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AyurvedaDbContext context,
        IEmailSender emailSender,
        IFileStorageService fileStorage,
        IConfiguration configuration,
        IOtpService otpService,
        ILoginThrottleService loginThrottle)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _emailSender = emailSender;
        _fileStorage = fileStorage;
        _configuration = configuration;
        _otpService = otpService;
        _loginThrottle = loginThrottle;
    }

    // --------------------------------------------------------------------------
    // 1. CUSTOMER & DOCTOR LOGIN
    // --------------------------------------------------------------------------

    [HttpGet]
    [Route("account/login")]
    [Route("doctor/login")]
    [Route("doctor-login")]
    [Route("doctors/login")]
    [Route("doctorportal/login")]
    [Route("doctor/signin")]
    public IActionResult Login(string? returnUrl = null, string? role = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var isDoctor = Request.Path.Value?.Contains("doctor") == true || string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase);
        ViewBag.GoogleSignInEnabled = !string.IsNullOrEmpty(_configuration["Authentication:Google:ClientId"]);
        ViewBag.DemoOpenAccess = _configuration.GetValue<bool>("Demo:OpenAccess", false)
            || _configuration.GetValue<bool>("Demo__OpenAccess", false)
            || _configuration.GetValue<bool>("Demo:AnyCredentialLogin", false)
            || _configuration.GetValue<bool>("Demo__AnyCredentialLogin", false);
        
        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            IsDoctorLogin = isDoctor
        });
    }

    [HttpPost]
    [Route("account/login")]
    [Route("doctor/login")]
    [Route("doctor-login")]
    [Route("doctors/login")]
    [Route("doctorportal/login")]
    [Route("doctor/signin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewBag.GoogleSignInEnabled = !string.IsNullOrEmpty(_configuration["Authentication:Google:ClientId"]);
        var isAnyCredentialLogin = _configuration.GetValue<bool>("Demo:AnyCredentialLogin", false) 
            || _configuration.GetValue<bool>("Demo__AnyCredentialLogin", false);
        ViewBag.DemoOpenAccess = isAnyCredentialLogin;

        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Check login throttle: 3 wrong attempts in 10 min, 15 failures in 1 hour
        var (isBlocked, remainingSeconds, _) = _loginThrottle.CheckLoginAllowed(clientIp, normalizedEmail);
        if (isBlocked)
        {
            ModelState.AddModelError(string.Empty, $"Too many failed login attempts. This account is locked out. Please try again in {remainingSeconds} seconds.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user != null && await _userManager.IsLockedOutAsync(user))
        {
            ModelState.AddModelError(string.Empty, "This account has been locked out due to multiple failed login attempts. Please try again later.");
            return View(model);
        }

        if (user != null && user.IsActive && !user.IsDeleted)
        {
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (isPasswordValid)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
                _loginThrottle.RecordSuccessfulLogin(clientIp, normalizedEmail);
                await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);
                await MergeGuestCartAsync(user.Id);

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
            else
            {
                await _userManager.AccessFailedAsync(user);
                if (await _userManager.IsLockedOutAsync(user))
                {
                    ModelState.AddModelError(string.Empty, "This account has been locked out due to multiple failed login attempts. Please try again later.");
                    return View(model);
                }
            }
        }

        // If user does not exist or password was invalid
        var (blocked, waitSecs, _) = await _loginThrottle.RecordFailedLoginAsync(clientIp, normalizedEmail);
        ModelState.AddModelError(string.Empty, blocked
            ? $"Too many failed login attempts. This account is locked out. Please try again in {waitSecs} seconds."
            : "Invalid email address or password. Please verify your credentials.");
        return View(model);
    }

    // --------------------------------------------------------------------------
    // 2. CUSTOMER SIGN UP WITH EMAIL OTP VERIFICATION
    // --------------------------------------------------------------------------

    [HttpGet]
    [Route("account/register")]
    public async Task<IActionResult> Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && await _userManager.IsInRoleAsync(user, "Doctor"))
            {
                // If currently signed in as a Doctor, sign out so the user can register separately as a customer
                await _signInManager.SignOutAsync();
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
        var isAnyCredentialLogin = _configuration.GetValue<bool>("Demo:AnyCredentialLogin", false) 
            || _configuration.GetValue<bool>("Demo__AnyCredentialLogin", false);
        ViewBag.DemoOpenAccess = isAnyCredentialLogin;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [Route("account/continue-demo-guest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContinueDemoGuest(string? returnUrl = null)
    {
        var randomId = Guid.NewGuid().ToString("N")[..6];
        var guestEmail = $"guest_{randomId}@demo.thegreatayurveda.com";
        var user = new ApplicationUser
        {
            UserName = guestEmail,
            Email = guestEmail,
            FullName = "Demo Guest",
            EmailConfirmed = true,
            IsActive = true,
            IsDemo = true,
            CreatedAt = DateTime.UtcNow
        };

        var createRes = await _userManager.CreateAsync(user, "DemoGuest@" + Guid.NewGuid().ToString("N")[..8] + "!");
        if (createRes.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, UserRole.Customer.ToString());
        }

        await CleanupDemoUsersAsync();
        await _signInManager.SignInAsync(user, isPersistent: true);
        await MergeGuestCartAsync(user.Id);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet, HttpPost]
    [Route("account/continue-demo-doctor")]
    [Route("doctor/continue-demo")]
    public async Task<IActionResult> ContinueDemoDoctor(string? returnUrl = null)
    {
        var openAccess = _configuration.GetValue<bool>("Demo:OpenAccess", false)
            || _configuration.GetValue<bool>("Demo__OpenAccess", false)
            || _configuration.GetValue<bool>("Demo:AnyCredentialLogin", false)
            || _configuration.GetValue<bool>("Demo__AnyCredentialLogin", false);

        if (!openAccess)
        {
            return Forbid();
        }

        var demoDoctor = await _userManager.FindByEmailAsync("demo.doctor@thegreatayurveda.com");
        if (demoDoctor == null)
        {
            demoDoctor = new ApplicationUser
            {
                UserName = "demo.doctor@thegreatayurveda.com",
                Email = "demo.doctor@thegreatayurveda.com",
                FullName = "Dr. Ananya Sharma (BAMS)",
                EmailConfirmed = true,
                IsActive = true,
                IsDemo = true,
                LockoutEnabled = false,
                CreatedAt = DateTime.UtcNow
            };
            var randomBytes = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
            var randomPwd = "Doc@" + Convert.ToHexString(randomBytes)[..12] + "!";
            var createRes = await _userManager.CreateAsync(demoDoctor, randomPwd);
            if (createRes.Succeeded)
            {
                await _userManager.AddToRoleAsync(demoDoctor, UserRole.Doctor.ToString());
            }
        }

        await _signInManager.SignInAsync(demoDoctor, isPersistent: true);

        if (!string.IsNullOrEmpty(returnUrl) && (Url.IsLocalUrl(returnUrl) || returnUrl.StartsWith("/")))
            return Redirect(returnUrl);

        return Redirect("/doctors/myappointments");
    }

    [HttpPost]
    [Route("account/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var isAnyCredentialLogin = _configuration.GetValue<bool>("Demo:AnyCredentialLogin", false) 
            || _configuration.GetValue<bool>("Demo__AnyCredentialLogin", false);
        ViewBag.DemoOpenAccess = isAnyCredentialLogin;

        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$") || normalizedEmail.EndsWith("@upi"))
        {
            ModelState.AddModelError("Email", "Please enter a valid email address with a domain (e.g. name@domain.com).");
            return View(model);
        }

        var existing = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existing != null)
        {
            if (existing.IsActive)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists. Please sign in instead.");
                return View(model);
            }
            else
            {
                // Account exists but is unverified/inactive. Update details, reset password, resend registration OTP.
                existing.FullName = model.FullName.Trim();
                existing.ContactNumber = model.ContactNumber.Trim();
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existing);
                await _userManager.ResetPasswordAsync(existing, resetToken, model.Password);
                await _userManager.UpdateAsync(existing);

                var (sent, message, _) = await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "Registration", clientIp, existing.FullName);
                if (!sent)
                {
                    TempData["WarningMessage"] = message;
                }
                TempData["InfoMessage"] = "A 6-digit registration verification code has been dispatched to your email.";
                return RedirectToAction("VerifyOtp", new { email = normalizedEmail });
            }
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            ContactNumber = model.ContactNumber.Trim(),
            EmailConfirmed = false,
            IsActive = false, // Strictly INACTIVE until OTP verified!
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, UserRole.Customer.ToString());

            // Generate and send 6-digit OTP for email verification with purpose "Registration"
            var (sent, message, _) = await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "Registration", clientIp, user.FullName);
            if (!sent)
            {
                TempData["WarningMessage"] = message;
            }

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
    public async Task<IActionResult> VerifyOtp(string email, string? returnUrl = null)
    {
        if (string.IsNullOrEmpty(email))
            return RedirectToAction("Login");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        ViewBag.IsDemoUser = user?.IsDemo == true;

        return View(new VerifyEmailOtpViewModel
        {
            Email = normalizedEmail,
            ReturnUrl = returnUrl,
            ResendCountdownSeconds = 60
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

        ViewBag.IsDemoUser = user.IsDemo;

        var isAnyCredentialLogin = _configuration.GetValue<bool>("Demo:AnyCredentialLogin", false) 
            || _configuration.GetValue<bool>("Demo__AnyCredentialLogin", false);
        bool allowOtpSkip = isAnyCredentialLogin && user.IsDemo && !string.IsNullOrWhiteSpace(model.Otp) && model.Otp.Trim().Length == 6;

        if (!allowOtpSkip)
        {
            var (valid, errorMsg) = await _otpService.ValidateOtpAsync(normalizedEmail, "Registration", model.Otp);
            if (!valid)
            {
                ModelState.AddModelError("Otp", errorMsg ?? "Invalid verification code. Please check and try again.");
                return View(model);
            }
        }

        // Activate the account upon successful verification
        user.IsActive = true;
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        await _signInManager.SignInAsync(user, isPersistent: true);
        TempData["SuccessMessage"] = "Email verified successfully! Your account is now active. Welcome to The Great Ayurveda.";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        if (await _userManager.IsInRoleAsync(user, "Doctor"))
        {
            var doc = await _context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id || d.Email == user.Email);
            if (doc?.DoctorDetail == null || doc.DoctorDetail.Status != 1)
            {
                return RedirectToAction("PendingApproval", "DoctorPortal");
            }
            return RedirectToAction("Dashboard", "DoctorPortal");
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Route("account/resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest req)
    {
        if (string.IsNullOrEmpty(req?.Email))
            return Json(new { success = false, message = "Email is required." });

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
            return Json(new { success = true, message = "A verification code has been dispatched if the account exists." });

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (sent, message, _) = await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "Registration", clientIp, user.FullName);

        return Json(new { success = sent, message });
    }

    // --------------------------------------------------------------------------
    // 4. DOCTOR ONBOARDING WIZARD & REGISTRATION (/doctor/register)
    // --------------------------------------------------------------------------

    [HttpGet]
    [Route("doctor/register")]
    [Route("account/doctor-register")]
    [Route("account/doctorregister")]
    public async Task<IActionResult> DoctorRegister()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && await _userManager.IsInRoleAsync(user, "Doctor"))
            {
                return RedirectToAction("Dashboard", "DoctorPortal");
            }
            await _signInManager.SignOutAsync();
        }
        ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        return View(new DoctorRegisterViewModel());
    }

    [HttpPost]
    [Route("doctor/register")]
    [Route("account/doctor-register")]
    [Route("account/doctorregister")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DoctorRegister(DoctorRegisterViewModel model)
    {
        ViewBag.Specializations = await _context.Categories.Where(c => c.IsActive).ToListAsync();

        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$") || normalizedEmail.EndsWith("@upi"))
        {
            ModelState.AddModelError("Email", "Please enter a valid email address with a domain (e.g. name@domain.com).");
            return View(model);
        }

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

        var autoApprove = _configuration.GetValue<bool>("Demo:AutoApproveDoctors", false)
            || _configuration.GetValue<bool>("Demo__AutoApproveDoctors", false);

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            ContactNumber = model.ContactNumber.Trim(),
            EmailConfirmed = false,
            IsActive = false, // Inactive until verified
            IsDemo = autoApprove,
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

        string? degreeDocUrl = null;
        if (model.DegreeCertificate != null && model.DegreeCertificate.Length > 0)
        {
            using var stream = model.DegreeCertificate.OpenReadStream();
            degreeDocUrl = await _fileStorage.SaveFileAsync(stream, model.DegreeCertificate.FileName, model.DegreeCertificate.ContentType);
        }

        var defaultCategory = await _context.Categories.FirstOrDefaultAsync();
        var specializationId = model.SpecializationId > 0 ? model.SpecializationId : (defaultCategory?.Id ?? 1);

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
                Status = autoApprove ? 1 : 0, // Auto-approved sandbox doctor if config is enabled
                IsDemoData = autoApprove,
                MorningTimesJson = "[\"09:00 AM\",\"10:00 AM\",\"11:00 AM\"]",
                EveningTimesJson = "[\"04:00 PM\",\"05:00 PM\",\"06:00 PM\"]"
            }
        };

        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync();

        // Invalidate doctor catalog cache so newly registered doctor appears immediately
        var cache = HttpContext.RequestServices.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        cache?.Remove("Doctors_Default_List");

        // Send OTP verification to doctor with purpose "Registration"
        await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "Registration", clientIp, user.FullName);
        var destUrl = autoApprove ? Url.Action("Dashboard", "DoctorPortal") : Url.Action("PendingApproval", "DoctorPortal");
        return RedirectToAction("VerifyOtp", new { email = normalizedEmail, returnUrl = destUrl });
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
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Safety: real admin/staff/seeded/protected accounts can never be verified or reset through demo OTP
        // (forgot password for them shows the same generic message and changes nothing).
        // Self-registered customers and doctors may use any-code reset.
        if (!await IsProtectedAccountAsync(normalizedEmail))
        {
            var user = await _userManager.FindByEmailAsync(normalizedEmail);
            if (user != null && !(user.Email?.StartsWith("guest_") ?? false) && !await _userManager.IsInRoleAsync(user, "DemoViewer"))
            {
                await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "ResetPassword", clientIp, user.FullName);
            }
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
        if (await IsProtectedAccountAsync(normalizedEmail))
        {
            ModelState.AddModelError(string.Empty, "Password reset failed. Please check the code or request a new one.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null || (user.Email?.StartsWith("guest_") ?? false) || await _userManager.IsInRoleAsync(user, "DemoViewer"))
        {
            ModelState.AddModelError(string.Empty, "Password reset failed. Please check the code or request a new one.");
            return View(model);
        }

        var (valid, errorMsg) = await _otpService.ValidateOtpAsync(normalizedEmail, "ResetPassword", model.Otp);
        if (!valid)
        {
            ModelState.AddModelError("Otp", errorMsg ?? "Invalid verification code.");
            return View(model);
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

        if (resetResult.Succeeded)
        {
            // Invalidate all active sessions across devices
            await _userManager.UpdateSecurityStampAsync(user);

            // Send password changed notification email
            await _emailSender.SendEmailAsync(
                user.Email!,
                "Security Alert: Your Password Was Changed - The Great Ayurveda",
                $"<p>Dear {user.FullName},</p><p>The password for your account (<strong>{user.Email}</strong>) was changed on {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC.</p><p>If you did not perform this change, please contact support immediately.</p>");

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

    [HttpGet]
    [Route("account/edit-profile")]
    [Route("account/change-password")]
    [Route("account/add-address")]
    [Route("account/edit-address")]
    [Route("account/set-default-address")]
    [Route("account/delete-address")]
    public IActionResult ProfileFormGetRedirect() => RedirectToAction(nameof(Profile));

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
    [Route("doctor/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var isDoctor = User.IsInRole("Doctor") || Request.Path.Value?.Contains("doctor") == true;
        var user = await _userManager.GetUserAsync(User);
        if (user != null && user.IsDemo && (user.Email ?? string.Empty).StartsWith("guest_"))
        {
            user.IsActive = false;
            user.CreatedAt = DateTime.UtcNow; // Purge timer starts on logout (24h)
            await _userManager.UpdateAsync(user);
        }

        await _signInManager.SignOutAsync();
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
    [ActionName("Logout")]
    [Route("account/logout")]
    [Route("doctor/logout")]
    public IActionResult LogoutGet()
    {
        // GET to logout must never 404 and must NOT change state
        var isDoctor = Request.Path.Value?.Contains("doctor") == true || User.IsInRole("Doctor");
        if (isDoctor)
        {
            return RedirectToAction("Login", "Account", new { role = "Doctor" });
        }

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

    private async Task<bool> IsProtectedAccountAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLowerInvariant();

        var protectedEmails = new[]
        {
            "admin@thegreatayurveda.com",
            "admin@ayurveda.com",
            "catalog@thegreatayurveda.com",
            "support@thegreatayurveda.com",
            "finance@thegreatayurveda.com",
            "customer@ayurveda.com",
            "anand@thegreatayurveda.com",
            "demo.doctor@thegreatayurveda.com",
            "demo-admin@ayurveda.local"
        };
        if (protectedEmails.Contains(normalized))
            return true;

        var user = await _userManager.FindByEmailAsync(normalized);
        if (user != null)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Any(r => r == "Admin" || r == "SuperAdmin" || r == "CatalogManager" ||
                               r == "FinanceManager" || r == "SupportAgent"))
            {
                return true;
            }
        }

        return false;
    }

    private async Task CleanupDemoUsersAsync()
    {
        try
        {
            // Demo guests kept at least 7 days if inactive; purged after 24h once logged out (IsActive == false)
            var cutoffActive = DateTime.UtcNow.AddDays(-7);
            var cutoffLoggedOut = DateTime.UtcNow.AddHours(-24);

            var exemptEmails = new[]
            {
                "customer@ayurveda.com",
                "anand@thegreatayurveda.com",
                "demo.doctor@thegreatayurveda.com",
                "demo-admin@ayurveda.local"
            };

            var toDelete = await _context.Users
                .Where(u => u.IsDemo && !exemptEmails.Contains(u.Email))
                .Where(u => (!u.IsActive && u.CreatedAt < cutoffLoggedOut) || (u.IsActive && u.CreatedAt < cutoffActive))
                .ToListAsync();

            if (toDelete.Any())
            {
                var userIds = toDelete.Select(u => u.Id).ToList();

                var carts = await _context.Carts.Where(c => userIds.Contains(c.UserId)).ToListAsync();
                var cartIds = carts.Select(c => c.Id).ToList();
                var cartItems = await _context.CartItems.Where(ci => cartIds.Contains(ci.CartId)).ToListAsync();
                _context.CartItems.RemoveRange(cartItems);
                _context.Carts.RemoveRange(carts);

                var orders = await _context.Orders.Where(o => userIds.Contains(o.UserId)).ToListAsync();
                var orderIds = orders.Select(o => o.Id).ToList();
                var orderItems = await _context.OrderItems.Where(oi => orderIds.Contains(oi.OrderId)).ToListAsync();
                _context.OrderItems.RemoveRange(orderItems);
                _context.Orders.RemoveRange(orders);

                var appts = await _context.Appointments.Where(a => userIds.Contains(a.UserId)).ToListAsync();
                _context.Appointments.RemoveRange(appts);

                var wishlists = await _context.WishlistItems.Where(w => userIds.Contains(w.UserId)).ToListAsync();
                _context.WishlistItems.RemoveRange(wishlists);

                var pReviews = await _context.ProductReviews.Where(r => userIds.Contains(r.UserId)).ToListAsync();
                _context.ProductReviews.RemoveRange(pReviews);

                var dReviews = await _context.DoctorReviews.Where(r => userIds.Contains(r.UserId)).ToListAsync();
                _context.DoctorReviews.RemoveRange(dReviews);

                var yReviews = await _context.YogaReviews.Where(r => userIds.Contains(r.UserId)).ToListAsync();
                _context.YogaReviews.RemoveRange(yReviews);

                var notifs = await _context.Notifications.Where(n => userIds.Contains(n.UserId)).ToListAsync();
                _context.Notifications.RemoveRange(notifs);

                var addresses = await _context.Addresses.Where(a => userIds.Contains(a.UserId)).ToListAsync();
                _context.Addresses.RemoveRange(addresses);

                var otps = await _context.PasswordResetOtps.Where(o => toDelete.Select(u => u.Email).Contains(o.Email)).ToListAsync();
                _context.PasswordResetOtps.RemoveRange(otps);

                _context.Users.RemoveRange(toDelete);
                await _context.SaveChangesAsync();
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private async Task MergeGuestCartAsync(int userId)
    {
        var sessionJson = HttpContext.Session.GetString("GuestCart_Items");
        var cookieJson = Request.Cookies[CartController.GuestCartCookieName];

        var combinedItems = new List<CartController.GuestCartItemDto>();
        try
        {
            if (!string.IsNullOrEmpty(sessionJson))
            {
                var sItems = System.Text.Json.JsonSerializer.Deserialize<List<CartController.GuestCartItemDto>>(sessionJson);
                if (sItems != null) combinedItems.AddRange(sItems);
            }
            if (!string.IsNullOrEmpty(cookieJson))
            {
                var cItems = System.Text.Json.JsonSerializer.Deserialize<List<CartController.GuestCartItemDto>>(cookieJson);
                if (cItems != null) combinedItems.AddRange(cItems);
            }

            if (combinedItems.Any())
            {
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new Cart { UserId = userId };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                // Consolidate duplicate products from both sources
                var groupedItems = combinedItems
                    .GroupBy(x => x.ProductId)
                    .Select(g => new CartController.GuestCartItemDto
                    {
                        ProductId = g.Key,
                        Quantity = g.Sum(x => x.Quantity)
                    }).ToList();

                foreach (var gi in groupedItems)
                {
                    var product = await _context.Products.FindAsync(gi.ProductId);
                    if (product == null || !product.IsActive) continue;

                    var existing = cart.CartItems.FirstOrDefault(ci => ci.ProductId == gi.ProductId);
                    if (existing != null)
                    {
                        existing.Quantity = Math.Min(product.Stock, existing.Quantity + gi.Quantity);
                    }
                    else
                    {
                        cart.CartItems.Add(new CartItem
                        {
                            CartId = cart.Id,
                            ProductId = gi.ProductId,
                            Quantity = Math.Min(product.Stock, Math.Max(1, gi.Quantity))
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }
        }
        catch
        {
            // Non-fatal
        }
        finally
        {
            HttpContext.Session.Remove("GuestCart_Items");
            Response.Cookies.Delete(CartController.GuestCartCookieName, new CookieOptions { Path = "/" });
        }
    }
}
