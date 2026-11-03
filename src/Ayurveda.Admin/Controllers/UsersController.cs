using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Policy = "RequireCustomerSupport")]
public class UsersController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public UsersController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    // -------------------------------------------------------------------------
    // 1. LIST & SEARCH PATIENTS / USERS
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Index(string? role, string? search, int page = 1)
    {
        int pageSize = 15;
        var q = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role);
            if (roleEntity != null)
            {
                var userIdsInRole = _context.UserRoles.Where(ur => ur.RoleId == roleEntity.Id).Select(ur => ur.UserId);
                q = q.Where(u => userIdsInRole.Contains(u.Id));
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(u => u.Email!.Contains(search) || u.FullName.Contains(search) || (u.ContactNumber != null && u.ContactNumber.Contains(search)));
        }

        var total = await q.CountAsync();
        var users = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var rolesMap = new Dictionary<int, string>();
        foreach (var u in users)
        {
            var userRoles = await _userManager.GetRolesAsync(u);
            rolesMap[u.Id] = userRoles.FirstOrDefault() ?? "Customer";
        }

        var model = new AdminUserListViewModel
        {
            Users = users,
            UserRoles = rolesMap,
            SelectedRole = role,
            SearchQuery = search,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            TotalUsers = total
        };

        return View(model);
    }

    // -------------------------------------------------------------------------
    // 2. USER PROFILE & DOSSIER DETAILS
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Details(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var orders = await _context.Orders.Where(o => o.UserId == id).OrderByDescending(o => o.OrderDate).ToListAsync();
        var consultations = await _context.Appointments.Where(a => a.UserId == id).OrderByDescending(a => a.StartUtc).ToListAsync();
        var addresses = await _context.Addresses.Where(a => a.UserId == id).ToListAsync();

        var model = new AdminUserDetailsViewModel
        {
            User = user,
            Role = roles.FirstOrDefault() ?? "Customer",
            Orders = orders,
            Consultations = consultations,
            Addresses = addresses
        };

        return View(model);
    }

    // -------------------------------------------------------------------------
    // 3. SUSPEND & UNSUSPEND ACCOUNT
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string? reason)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user != null)
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(10));
            await _auditService.LogAsync("UserSuspended", "ApplicationUser", id.ToString(), $"Suspended user {user.Email}. Reason: {reason ?? "Admin discretion"}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"User {user.Email} has been locked and suspended.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsuspend(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user != null)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _auditService.LogAsync("UserUnsuspended", "ApplicationUser", id.ToString(), $"Reactivated user {user.Email}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"User {user.Email} has been unlocked and restored.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------------------------------------
    // 4. FORCE PASSWORD RESET
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForcePasswordReset(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, "AyurReset@2026");
            if (resetResult.Succeeded)
            {
                await _auditService.LogAsync("ForcePasswordReset", "ApplicationUser", id.ToString(), $"Forced password reset for {user.Email}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
                TempData["SuccessMessage"] = $"Temporary password 'AyurReset@2026' has been assigned to {user.Email}.";
            }
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------------------------------------
    // 5. GDPR / DPDP DATA EXPORT (JSON DOWNLOAD)
    // -------------------------------------------------------------------------
    public async Task<IActionResult> ExportData(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        var orders = await _context.Orders.Include(o => o.OrderItems).Where(o => o.UserId == id).ToListAsync();
        var consultations = await _context.Appointments.Where(a => a.UserId == id).ToListAsync();
        var addresses = await _context.Addresses.Where(a => a.UserId == id).ToListAsync();

        var exportPayload = new
        {
            ExportedAtUtc = DateTime.UtcNow,
            RegulationCompliance = "DPDP Act 2023 / GDPR Data Portability",
            Profile = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.ContactNumber,
                user.Gender,
                user.CreatedAt
            },
            Addresses = addresses.Select(a => new { a.RecipientName, a.Phone, a.AddressLine1, a.City, a.State, a.PostalCode }),
            Orders = orders.Select(o => new { o.OrderNumber, o.OrderDate, o.TotalAmount, o.Status, Items = o.OrderItems.Select(i => new { i.ProductId, i.Quantity, i.UnitPrice }) }),
            Consultations = consultations.Select(c => new { c.Id, c.AppointmentDate, c.ConsultationType, c.HealthConcern, c.Status })
        };

        await _auditService.LogAsync("UserDataExported", "ApplicationUser", id.ToString(), $"Exported personal data archive for {user.Email}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        var json = JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"userData_{user.Id}_{DateTime.UtcNow:yyyyMMdd}.json");
    }

    // -------------------------------------------------------------------------
    // 6. RIGHT TO BE FORGOTTEN / ANONYMIZATION
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Anonymize(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            var oldEmail = user.Email;
            user.FullName = "Anonymized Patient";
            user.Email = $"anonymized_{user.Id}@thegreatayurveda.deleted";
            user.UserName = user.Email;
            user.NormalizedEmail = user.Email.ToUpper();
            user.NormalizedUserName = user.Email.ToUpper();
            user.ContactNumber = "+91 00000 00000";

            var addresses = await _context.Addresses.Where(a => a.UserId == id).ToListAsync();
            foreach (var addr in addresses)
            {
                addr.RecipientName = "Redacted";
                addr.Phone = "0000000000";
                addr.AddressLine1 = "Redacted Address";
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("UserAnonymized", "ApplicationUser", id.ToString(), $"Anonymized PII for former user {oldEmail} pursuant to DPDP/GDPR", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["SuccessMessage"] = $"User #{id} PII has been scrubbed and anonymized.";
        }
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 7. EDIT USER
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var model = new AdminUserEditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            ContactNumber = user.ContactNumber,
            Gender = user.Gender,
            DateOfBirth = user.DateOfBirth,
            Role = roles.FirstOrDefault() ?? "Customer",
            IsActive = user.IsActive && (!user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow),
            IsDeleted = user.IsDeleted
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Edit(int id, AdminUserEditViewModel model)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.FullName))
        {
            ModelState.AddModelError(nameof(model.FullName), "Full Name is required.");
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.ContactNumber = model.ContactNumber?.Trim();
        user.Gender = model.Gender?.Trim();
        user.DateOfBirth = model.DateOfBirth;
        user.IsActive = model.IsActive;

        if (!model.IsActive)
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
        }

        await _userManager.UpdateAsync(user);

        // Role update
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!string.IsNullOrWhiteSpace(model.Role) && !currentRoles.Contains(model.Role))
        {
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }
            await _userManager.AddToRoleAsync(user, model.Role);
        }

        await _auditService.LogAsync("UserUpdated", "ApplicationUser", id.ToString(), $"Updated user details for {user.Email}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"User profile for {user.Email} updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------------------------------------
    // 8. BLOCK / UNBLOCK USER
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user != null)
        {
            user.IsActive = false;
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            await _userManager.UpdateAsync(user);
            await _auditService.LogAsync("UserBlocked", "ApplicationUser", id.ToString(), $"Blocked user {user.Email}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"User {user.Email} has been blocked from logging in.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user != null)
        {
            user.IsActive = true;
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.UpdateAsync(user);
            await _auditService.LogAsync("UserUnblocked", "ApplicationUser", id.ToString(), $"Unblocked user {user.Email}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"User {user.Email} has been unblocked and active.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------------------------------------
    // 9. SAFE DELETE ACCOUNT (SOFT DELETE IF REFERENCED, HARD DELETE IF CLEAN)
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user != null)
        {
            var email = user.Email;

            // Check all referencing entities to strictly prevent FK 500 errors
            var hasOrders = await _context.Orders.AnyAsync(o => o.UserId == id);
            var hasAppointments = await _context.Appointments.AnyAsync(a => a.UserId == id);
            var hasReviews = await _context.DoctorReviews.AnyAsync(r => r.UserId == id);

            if (hasOrders || hasAppointments || hasReviews)
            {
                // Soft delete: keep history, prevent login, hide everywhere
                user.IsDeleted = true;
                user.IsActive = false;
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                await _userManager.UpdateAsync(user);

                await _auditService.LogAsync("UserSoftDeleted", "ApplicationUser", id.ToString(), $"Soft-deleted user {email} (preserved referenced history)", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
                TempData["SuccessMessage"] = $"User {email} has referenced order/consultation history and was safely soft-deleted (login blocked, history preserved).";
            }
            else
            {
                // Unreferenced: clean up addresses and hard delete
                var addresses = await _context.Addresses.Where(a => a.UserId == id).ToListAsync();
                if (addresses.Any())
                {
                    _context.Addresses.RemoveRange(addresses);
                    await _context.SaveChangesAsync();
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    await _auditService.LogAsync("UserHardDeleted", "ApplicationUser", id.ToString(), $"Permanently deleted unreferenced user {email}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
                    TempData["SuccessMessage"] = $"User {email} had no references and was permanently deleted.";
                }
                else
                {
                    user.IsDeleted = true;
                    user.IsActive = false;
                    await _userManager.UpdateAsync(user);
                    TempData["SuccessMessage"] = $"User {email} was soft-deleted.";
                }
            }
        }
        return RedirectToAction(nameof(Index));
    }
}
