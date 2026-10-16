using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;

namespace Ayurveda.Admin.Controllers;

[Area("Admin")]
public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
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
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        // Generic error message to prevent enumeration
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid administrative credentials.");
            return View(model);
        }

        // Verify that the user has an administrative role
        var roles = await _userManager.GetRolesAsync(user);
        var adminRoles = new[] { "SuperAdmin", "CatalogManager", "SupportAgent", "FinanceManager", "Admin", "DemoViewer" };
        if (!roles.Any(r => adminRoles.Contains(r)))
        {
            ModelState.AddModelError(string.Empty, "Access restricted to authorized administrative personnel.");
            return View(model);
        }

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

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordValid)
        {
            ModelState.AddModelError(string.Empty, "Invalid administrative credentials.");
            return View(model);
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

    [Authorize(AuthenticationSchemes = "AdminCookieScheme")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin-portal/logout")]
    [Route("admin-portal/auth/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminCookieScheme");
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
    [Route("admin-portal/logout")]
    [Route("admin-portal/auth/logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await HttpContext.SignOutAsync("AdminCookieScheme");
        try
        {
            HttpContext.Session?.Clear();
        }
        catch
        {
        }
        TempData["SuccessMessage"] = "Administrative session terminated successfully.";
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
}
