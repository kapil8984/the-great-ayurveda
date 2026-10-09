using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;

namespace Ayurveda.Admin.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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
        if (user == null || !user.IsActive)
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

        // If user has 2FA enabled and TOTP code provided
        if (await _userManager.GetTwoFactorEnabledAsync(user) && !string.IsNullOrEmpty(model.TotpCode))
        {
            var validCode = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, model.TotpCode.Trim());
            if (!validCode)
            {
                ModelState.AddModelError(string.Empty, "Invalid two-factor authenticator code.");
                return View(model);
            }
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Store audit log timestamp
            TempData["LastLoginNotice"] = $"Last authenticated session: {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC";

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Security Alert: Administrative account locked for 15 minutes due to multiple failed authentication attempts.");
            return View(model);
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
}
