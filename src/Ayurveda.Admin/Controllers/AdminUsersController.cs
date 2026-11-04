using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Policy = "RequireSuperAdmin")]
public class AdminUsersController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IOtpService _otpService;

    public AdminUsersController(
        AyurvedaDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IOtpService otpService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
        _otpService = otpService;
    }

    public async Task<IActionResult> Index()
    {
        var adminRoles = new[] { "SuperAdmin", "CatalogManager", "SupportAgent", "FinanceManager", "Admin" };
        var users = await _context.Users.ToListAsync();

        var adminStaff = new List<(ApplicationUser User, string Role)>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var adminRole = roles.FirstOrDefault(r => adminRoles.Contains(r));
            if (adminRole != null)
            {
                adminStaff.Add((u, adminRole));
            }
        }

        return View(adminStaff);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, string fullName, string role, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            TempData["ErrorMessage"] = "Email and password are required.";
            return RedirectToAction(nameof(Index));
        }

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            TempData["ErrorMessage"] = "A user with this email address already exists.";
            return RedirectToAction(nameof(Index));
        }

        var newUser = new ApplicationUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            FullName = fullName.Trim(),
            EmailConfirmed = false, // Strictly unconfirmed until OTP is verified
            IsActive = false,        // Strictly inactive until OTP is verified
            CreatedAt = DateTime.UtcNow
        };

        var createRes = await _userManager.CreateAsync(newUser, password);
        if (!createRes.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("; ", createRes.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        await _userManager.AddToRoleAsync(newUser, role);

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var (sent, message, _) = await _otpService.GenerateAndSendOtpAsync(newUser.Email, "Registration", clientIp, newUser.FullName);

        await _auditService.LogAsync("AdminStaffCreated", "AdminUser", newUser.Id.ToString(), $"Provisioned administrative staff '{newUser.Email}' with role '{role}' (Pending OTP Verification)", User.Identity?.Name, clientIp);

        TempData["SuccessMessage"] = $"Administrative staff user {newUser.Email} ({role}) provisioned. A 6-digit registration OTP has been dispatched to {newUser.Email}. The account will remain inactive until OTP verification is completed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyStaffOtp(string email, string otp)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
        {
            TempData["ErrorMessage"] = "Email and 6-digit verification code are required.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Staff user account not found.";
            return RedirectToAction(nameof(Index));
        }

        var (success, msg) = await _otpService.VerifyOtpAsync(normalizedEmail, "Registration", otp.Trim());
        if (!success)
        {
            TempData["ErrorMessage"] = msg;
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = true;
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        await _auditService.LogAsync("AdminStaffActivated", "AdminUser", user.Id.ToString(), $"Staff account '{user.Email}' activated via OTP verification.", User.Identity?.Name, clientIp);

        TempData["SuccessMessage"] = $"Administrative staff user {user.Email} successfully verified and activated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendStaffOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Email address is required.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Staff user account not found.";
            return RedirectToAction(nameof(Index));
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
        return RedirectToAction(nameof(Index));
    }
}
