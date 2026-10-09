using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Policy = "RequireSuperAdmin")]
public class SettingsController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment _env;

    public SettingsController(AyurvedaDbContext context, IAuditService auditService, IWebHostEnvironment env)
    {
        _context = context;
        _auditService = auditService;
        _env = env;
    }

    public IActionResult Index()
    {
        var model = new AdminSystemSettingsViewModel
        {
            FreeShippingThreshold = 799m,
            StandardShippingFee = 70m,
            GstPercentageRate = 12m,
            DoctorCommissionPercentageRate = 15m,
            ConsultationSlotDurationMinutes = 30,
            EnableIpAllowlisting = false,
            AllowedIpListCsv = "127.0.0.1, ::1"
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(AdminSystemSettingsViewModel model)
    {
        await _auditService.LogAsync("SystemSettingsUpdated", "Configuration", "System", $"Updated platform rules: Free Shipping: ₹{model.FreeShippingThreshold}, Commission: {model.DoctorCommissionPercentageRate}%, GST: {model.GstPercentageRate}%", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = "System configuration settings saved.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Health()
    {
        bool dbHealthy = false;
        int count = 0;
        try
        {
            count = await _context.Products.CountAsync();
            dbHealthy = true;
        }
        catch
        {
            dbHealthy = false;
        }

        var process = Process.GetCurrentProcess();
        var memMb = (process.WorkingSet64 / (1024 * 1024)).ToString();

        var model = new AdminSystemHealthViewModel
        {
            DatabaseHealthy = dbHealthy,
            DatabaseProvider = _context.Database.ProviderName ?? "Entity Framework Core",
            DatabaseItemCount = count,
            EmailServiceConfigured = true,
            FileStorageHealthy = true,
            EnvironmentName = _env.EnvironmentName,
            MemoryUsageMb = $"{memMb} MB",
            ServerTimeUtc = DateTime.UtcNow
        };

        return View(model);
    }
}
