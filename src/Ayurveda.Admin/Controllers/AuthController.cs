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

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked out due to multiple failed access attempts. Please try again after 15 minutes.");
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
        TempData["SuccessMessage"] = "Admin session terminated.";
        return RedirectToAction(nameof(Login));
    }
}
