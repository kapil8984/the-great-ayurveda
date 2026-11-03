using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;

namespace Ayurveda.Admin.Controllers;

[Area("Admin")]
public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly ILoginThrottleService _loginThrottle;
    private readonly IOtpService _otpService;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        ILoginThrottleService loginThrottle,
        IOtpService otpService,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _loginThrottle = loginThrottle;
        _otpService = otpService;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    [HttpGet]
    [Route("admin-portal/login")]
    [Route("admin-portal/auth/login")]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var auth = await HttpContext.AuthenticateAsync("AdminCookieScheme");
        if (auth.Succeeded && auth.Principal?.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
        return View(new AdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin-portal/login")]
    [Route("admin-portal/auth/login")]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Check throttle: 3 wrong attempts in 10 min, 15 failures in 1 hour
        var (isBlocked, remainingSeconds, _) = _loginThrottle.CheckLoginAllowed(clientIp, normalizedEmail);
        if (isBlocked)
        {
            ModelState.AddModelError(string.Empty, $"Too many failed login attempts. Please try again in {remainingSeconds} seconds.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        // Generic error message to prevent enumeration
        if (user == null || !user.IsActive)
        {
            var (blocked, waitSecs, _) = await _loginThrottle.RecordFailedLoginAsync(clientIp, normalizedEmail);
            ModelState.AddModelError(string.Empty, blocked
                ? $"Too many failed login attempts. Please try again in {waitSecs} seconds."
                : "Invalid administrative credentials.");
            return View(model);
        }

        // Verify that the user has an administrative role
        var roles = await _userManager.GetRolesAsync(user);
        var adminRoles = new[] { "SuperAdmin", "CatalogManager", "SupportAgent", "FinanceManager", "Admin", "DemoViewer" };
        if (!roles.Any(r => adminRoles.Contains(r)))
        {
            var (blocked, waitSecs, _) = await _loginThrottle.RecordFailedLoginAsync(clientIp, normalizedEmail);
            ModelState.AddModelError(string.Empty, blocked
                ? $"Too many failed login attempts. Please try again in {waitSecs} seconds."
                : "Access restricted to authorized administrative personnel.");
            return View(model);
        }

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordValid)
        {
            var (blocked, waitSecs, _) = await _loginThrottle.RecordFailedLoginAsync(clientIp, normalizedEmail);
            ModelState.AddModelError(string.Empty, blocked
                ? $"Too many failed login attempts. Please try again in {waitSecs} seconds."
                : "Invalid administrative credentials.");
            return View(model);
        }

        // Reset throttle upon successful authentication
        _loginThrottle.RecordSuccessfulLogin(clientIp, normalizedEmail);

        // Map legacy "Admin" role to "SuperAdmin" at login
        if (roles.Contains("Admin") && !roles.Contains("SuperAdmin"))
        {
            if (!await _roleManager.RoleExistsAsync("SuperAdmin"))
            {
                await _roleManager.CreateAsync(new IdentityRole<int>("SuperAdmin"));
            }
            await _userManager.AddToRoleAsync(user, "SuperAdmin");
            roles = await _userManager.GetRolesAsync(user);
        }

        // In Development/Portfolio build: ensure lockout is cleared for admin
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        // Build Admin Principal with AdminCookieScheme
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        // Ensure legacy Admin role maps to SuperAdmin claim
        if (roles.Contains("Admin") && !identity.HasClaim(ClaimTypes.Role, "SuperAdmin"))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "SuperAdmin"));
        }

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync("AdminCookieScheme", principal, authProperties);

        // Store audit log timestamp
        TempData["LastLoginNotice"] = $"Last authenticated session: {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    [HttpGet, HttpPost]
    [Route("admin-portal/continue-demo")]
    [Route("admin-portal/auth/continue-demo")]
    public async Task<IActionResult> ContinueDemoAdmin(string? returnUrl = null)
    {
        var openAccess = _configuration.GetValue<bool>("Demo:OpenAccess", false)
            || _configuration.GetValue<bool>("Demo__OpenAccess", false);
        if (!openAccess)
        {
            return Forbid();
        }

        var demoViewer = await _userManager.FindByEmailAsync("demo-admin@ayurveda.local");
        if (demoViewer == null)
        {
            demoViewer = new ApplicationUser
            {
                UserName = "demo-admin@ayurveda.local",
                Email = "demo-admin@ayurveda.local",
                FullName = "Demo Admin Viewer",
                EmailConfirmed = true,
                IsActive = true,
                IsDemo = true,
                CreatedAt = DateTime.UtcNow
            };
            var randomBytes = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
            var randomPwd = "Adm@" + Convert.ToHexString(randomBytes)[..12] + "!";
            await _userManager.CreateAsync(demoViewer, randomPwd);
            await _userManager.AddToRolesAsync(demoViewer, new[] { "Admin", "DemoViewer" });
        }

        var principal = await _signInManager.CreateUserPrincipalAsync(demoViewer);
        var identity = (ClaimsIdentity)principal.Identity!;
        if (!identity.HasClaim(ClaimTypes.Role, "DemoViewer"))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "DemoViewer"));
        }
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };
        await HttpContext.SignInAsync("AdminCookieScheme", principal, authProperties);
        TempData["SuccessMessage"] = "Demo admin portal access granted (read-only mode).";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    [HttpGet]
    [Route("admin-portal/register")]
    [Route("admin-portal/auth/register")]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin-portal/register")]
    [Route("admin-portal/auth/register")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
        {
            ModelState.AddModelError("Email", "Please enter a valid email address with a domain (e.g. name@domain.com).");
            return View(model);
        }

        var protectedAccounts = new[] { "admin@thegreatayurveda.com", "admin@ayurveda.com", "catalog@thegreatayurveda.com", "support@thegreatayurveda.com", "finance@thegreatayurveda.com" };
        if (protectedAccounts.Contains(normalizedEmail))
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

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
            FullName = "Demo admin (read-only)",
            EmailConfirmed = false,
            IsActive = true,
            IsDemo = true,
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

        if (!await _roleManager.RoleExistsAsync("DemoViewer"))
        {
            await _roleManager.CreateAsync(new IdentityRole<int>("DemoViewer"));
        }
        await _userManager.AddToRoleAsync(user, "DemoViewer");

        await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "AdminVerifyEmail", clientIp, user.FullName);
        return RedirectToAction(nameof(VerifyOtp), new { email = normalizedEmail });
    }

    [HttpGet]
    [Route("admin-portal/verify-otp")]
    [Route("admin-portal/auth/verify-otp")]
    public IActionResult VerifyOtp(string email)
    {
        return View(new VerifyEmailOtpViewModel { Email = email, ResendCountdownSeconds = 60 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin-portal/verify-otp")]
    [Route("admin-portal/auth/verify-otp")]
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

        var (valid, errorMsg) = await _otpService.ValidateOtpAsync(normalizedEmail, "AdminVerifyEmail", model.Otp);
        if (!valid)
        {
            ModelState.AddModelError("Otp", errorMsg ?? "Invalid verification code.");
            return View(model);
        }

        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;
        if (!identity.HasClaim(ClaimTypes.Role, "DemoViewer"))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "DemoViewer"));
        }

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await HttpContext.SignInAsync("AdminCookieScheme", principal, authProperties);
        TempData["SuccessMessage"] = "Demo admin portal access granted (read-only mode).";
        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    [HttpGet]
    [Route("admin-portal/forgot-password")]
    [Route("admin-portal/auth/forgot-password")]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin-portal/forgot-password")]
    [Route("admin-portal/auth/forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        // Exclude DemoViewer and demo guests
        if (user != null && !user.IsDemo && !user.Email.StartsWith("guest_") && !await _userManager.IsInRoleAsync(user, "DemoViewer"))
        {
            var roles = await _userManager.GetRolesAsync(user);
            var adminRoles = new[] { "SuperAdmin", "CatalogManager", "SupportAgent", "FinanceManager", "Admin" };
            if (roles.Any(r => adminRoles.Contains(r)))
            {
                await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "ResetPassword", clientIp, user.FullName);
            }
        }

        TempData["SuccessMessage"] = "If an administrative account exists with this email, a 6-digit verification code has been dispatched.";
        return RedirectToAction(nameof(ResetPassword), new { email = normalizedEmail });
    }

    [HttpGet]
    [Route("admin-portal/reset-password")]
    [Route("admin-portal/auth/reset-password")]
    public IActionResult ResetPassword(string? email)
    {
        return View(new ResetPasswordOtpViewModel { Email = email ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin-portal/reset-password")]
    [Route("admin-portal/auth/reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordOtpViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null || user.IsDemo || user.Email.StartsWith("guest_") || await _userManager.IsInRoleAsync(user, "DemoViewer"))
        {
            ModelState.AddModelError(string.Empty, "Password reset failed. Please verify the code or request a new one.");
            return View(model);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var adminRoles = new[] { "SuperAdmin", "CatalogManager", "SupportAgent", "FinanceManager", "Admin" };
        if (!roles.Any(r => adminRoles.Contains(r)))
        {
            ModelState.AddModelError(string.Empty, "Password reset failed. Please verify the code or request a new one.");
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
                "Security Alert: Admin Password Changed - The Great Ayurveda",
                $"<p>Dear {user.FullName},</p><p>The administrative password for your account (<strong>{user.Email}</strong>) was changed on {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC.</p><p>If you did not perform this change, please contact the security team immediately.</p>");

            TempData["SuccessMessage"] = "Your administrative password has been reset successfully. Please sign in with your new credentials.";
            return RedirectToAction(nameof(Login));
        }

        foreach (var error in resetResult.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [Route("admin-portal/auth/resend-otp")]
    [Route("admin-portal/resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest req)
    {
        if (string.IsNullOrEmpty(req?.Email))
            return Json(new { success = false, message = "Email required." });

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null || user.IsDemo || await _userManager.IsInRoleAsync(user, "DemoViewer"))
            return Json(new { success = true, message = "Code dispatched if account exists." });

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (sent, message, _) = await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "ResetPassword", clientIp, user.FullName);
        return Json(new { success = sent, message });
    }

    [Authorize(AuthenticationSchemes = "AdminCookieScheme")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin-portal/logout")]
    [Route("admin-portal/auth/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminCookieScheme");
        TempData["SuccessMessage"] = "Administrative session terminated successfully.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [ActionName("Logout")]
    [Route("admin-portal/logout")]
    [Route("admin-portal/auth/logout")]
    [AllowAnonymous]
    public IActionResult LogoutGet()
    {
        // GET to logout must never 404 and must NOT change state
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Route("admin-portal/accessdenied")]
    [Route("admin-portal/auth/accessdenied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [Authorize(AuthenticationSchemes = "AdminCookieScheme")]
    [HttpGet]
    [Route("admin-portal/account/whoami")]
    [Route("admin-portal/auth/whoami")]
    public IActionResult WhoAmI()
    {
        var email = User.Identity?.Name ?? "Unknown";
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        var isSuperAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var isDemoViewer = User.IsInRole("DemoViewer");
        var isCatalog = isSuperAdmin || isDemoViewer || User.IsInRole("CatalogManager");
        var isSupport = isSuperAdmin || isDemoViewer || User.IsInRole("SupportAgent");
        var isFinance = isSuperAdmin || isDemoViewer || User.IsInRole("FinanceManager");

        var menuItems = new[]
        {
            new { Item = "Dashboard", Policy = "None", Visible = true },
            new { Item = "Doctor Practice", Policy = "RequireCustomerSupport", Visible = isSupport },
            new { Item = "Doctor Verification", Policy = "RequireCustomerSupport", Visible = isSupport },
            new { Item = "Products & Stock", Policy = "RequireCatalogManagement", Visible = isCatalog },
            new { Item = "Categories Catalog", Policy = "RequireCatalogManagement", Visible = isCatalog },
            new { Item = "Coupons & Offers", Policy = "RequireCatalogManagement", Visible = isCatalog },
            new { Item = "Store Banners", Policy = "RequireCatalogManagement", Visible = isCatalog },
            new { Item = "Yoga & Wellness", Policy = "RequireCatalogManagement", Visible = isCatalog },
            new { Item = "Orders & Dispatch", Policy = "RequireCustomerSupport / RequireFinanceManagement", Visible = (isSupport || isFinance) },
            new { Item = "Tele-Consultations", Policy = "RequireCustomerSupport", Visible = isSupport },
            new { Item = "Reviews & Q&A", Policy = "RequireCustomerSupport", Visible = isSupport },
            new { Item = "Users & Patients", Policy = "RequireCustomerSupport", Visible = isSupport },
            new { Item = "Finance & GST", Policy = "RequireFinanceManagement", Visible = isFinance },
            new { Item = "Financial Reports", Policy = "RequireFinanceManagement", Visible = isFinance },
            new { Item = "Staff Roles & 2FA", Policy = "RequireSuperAdmin", Visible = isSuperAdmin || isDemoViewer },
            new { Item = "Audit Trail", Policy = "RequireSuperAdmin", Visible = isSuperAdmin || isDemoViewer },
            new { Item = "System Settings", Policy = "RequireSuperAdmin", Visible = isSuperAdmin || isDemoViewer }
        };

        if (Request.Headers["Accept"].ToString().Contains("application/json") || Request.Query.ContainsKey("json"))
        {
            return Json(new { email, roles, menuItems });
        }

        ViewBag.Email = email;
        ViewBag.Roles = roles;
        ViewBag.MenuItems = menuItems;
        return View();
    }
}
