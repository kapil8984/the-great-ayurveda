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

    public AdminUsersController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
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
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createRes = await _userManager.CreateAsync(newUser, password);
        if (!createRes.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("; ", createRes.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        await _userManager.AddToRoleAsync(newUser, role);

        await _auditService.LogAsync("AdminStaffCreated", "AdminUser", newUser.Id.ToString(), $"Created administrative staff '{newUser.Email}' with role '{role}'", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Administrative staff user {newUser.Email} ({role}) provisioned.";
        return RedirectToAction(nameof(Index));
    }
}
