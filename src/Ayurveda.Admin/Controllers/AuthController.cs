using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;

namespace Ayurveda.Admin.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly IOtpService _otpService;
    private readonly IAuditService _auditService;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        IOtpService otpService,
        IAuditService auditService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _otpService = otpService;
        _auditService = auditService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View(new AdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        // Generic error message to prevent enumeration
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid administrative credentials.");
            return View(model);
        }

        // Verify that the user has an administrative role
        var roles = await _userManager.GetRolesAsync(user);
        var adminRoles = new[] { "SuperAdmin", "CatalogManager", "SupportAgent", "FinanceManager", "Admin" };
        if (!roles.Any(r => adminRoles.Contains(r)))
        {
            ModelState.AddModelError(string.Empty, "Access restricted to authorized administrative personnel.");
            return View(model);
        }

        // Check if admin account is pending OTP verification
        if (!user.IsActive || !user.EmailConfirmed)
        {
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (isPasswordValid)
            {
                TempData["WarningMessage"] = "Your administrative account is pending OTP verification. Please enter the 6-digit code sent to your corporate email.";
                return RedirectToAction(nameof(VerifyOtp), new { email = normalizedEmail, returnUrl = model.ReturnUrl });
            }

            ModelState.AddModelError(string.Empty, "Invalid administrative credentials.");
            return View(model);
        }

        // Item 2: Map legacy "Admin" role to "SuperAdmin" at login
        if (roles.Contains("Admin") && !roles.Contains("SuperAdmin"))
        {
            if (!await _roleManager.RoleExistsAsync("SuperAdmin"))
            {
                await _roleManager.CreateAsync(new IdentityRole<int>("SuperAdmin"));
            }
            await _userManager.AddToRoleAsync(user, "SuperAdmin");
        }

        // In Development/Portfolio build: ensure lockout is cleared for admin
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // Store audit log timestamp
            TempData["LastLoginNotice"] = $"Last authenticated session: {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC";

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, "Invalid administrative credentials.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        try
        {
            HttpContext.Session?.Clear();
        }
        catch
        {
            // Session may not be configured in test or limited host environments
        }
        TempData["SuccessMessage"] = "Administrative session terminated successfully.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [ActionName("Logout")]
    public IActionResult LogoutGet()
    {
        // Logout is strictly POST with anti-forgery token. Never allow GET logout links.
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult VerifyOtp(string? email, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(Login));

        ViewBag.Email = email.Trim().ToLowerInvariant();
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(string email, string otp, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
        {
            ModelState.AddModelError(string.Empty, "Email and 6-digit verification code are required.");
            ViewBag.Email = email;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Administrative account not found.");
            ViewBag.Email = email;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var (success, message) = await _otpService.VerifyOtpAsync(normalizedEmail, "Registration", otp.Trim());
        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            ViewBag.Email = email;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        user.IsActive = true;
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        await _auditService.LogAsync("AdminAccountActivated", "AdminUser", user.Id.ToString(), $"Admin user '{user.Email}' completed registration OTP verification and activated account.", user.Email, clientIp);

        await _signInManager.SignInAsync(user, isPersistent: true);
        TempData["SuccessMessage"] = "Administrative account verified and activated successfully.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Email address is required.";
            return RedirectToAction(nameof(Login));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            TempData["SuccessMessage"] = "A 6-digit verification code has been dispatched if the account exists.";
            return RedirectToAction(nameof(VerifyOtp), new { email = normalizedEmail });
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var (sent, msg, _) = await _otpService.GenerateAndSendOtpAsync(normalizedEmail, "Registration", clientIp, user.FullName);
        if (sent)
        {
            TempData["SuccessMessage"] = $"A fresh 6-digit registration code has been dispatched to {normalizedEmail}.";
        }
        else
        {
            TempData["ErrorMessage"] = msg;
        }

        return RedirectToAction(nameof(VerifyOtp), new { email = normalizedEmail });
    }
}
