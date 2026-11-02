using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;

namespace Ayurveda.Admin.Controllers;

[Authorize(Policy = "RequireSuperAdmin")]
public class CatalogImportController : Controller
{
    private readonly ICatalogImportService _catalogImportService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CatalogImportController> _logger;

    public CatalogImportController(
        ICatalogImportService catalogImportService,
        IConfiguration configuration,
        ILogger<CatalogImportController> logger)
    {
        _catalogImportService = catalogImportService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.CatalogImportEnabled = _configuration.GetValue<bool>("CatalogImport:Enabled", true);
        var status = await _catalogImportService.GetSystemStatusAsync(ct);
        return View(status);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewImport(CancellationToken ct)
    {
        if (!_configuration.GetValue<bool>("CatalogImport:Enabled", true))
        {
            return Json(new { success = false, message = "Catalogue import is disabled by configuration (CatalogImport:Enabled=false)." });
        }

        var preview = await _catalogImportService.PreviewProductImportAsync(ct);
        return Json(new { success = true, data = preview });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(CancellationToken ct)
    {
        if (!_configuration.GetValue<bool>("CatalogImport:Enabled", true))
        {
            return Json(new { success = false, message = "Catalogue import is disabled by configuration (CatalogImport:Enabled=false)." });
        }

        var userEmail = User.Identity?.Name ?? "SuperAdmin";
        _logger.LogInformation("SuperAdmin {User} confirmed execution of product catalogue import.", userEmail);

        var result = await _catalogImportService.ExecuteProductImportAsync(userEmail, ct);
        return Json(new { success = result.Success, data = result });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewImageMapping(CancellationToken ct)
    {
        if (!_configuration.GetValue<bool>("CatalogImport:Enabled", true))
        {
            return Json(new { success = false, message = "Image mapping is disabled by configuration (CatalogImport:Enabled=false)." });
        }

        var preview = await _catalogImportService.PreviewImageMappingAsync(ct);
        return Json(new { success = true, data = preview });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImageMapping(CancellationToken ct)
    {
        if (!_configuration.GetValue<bool>("CatalogImport:Enabled", true))
        {
            return Json(new { success = false, message = "Image mapping is disabled by configuration (CatalogImport:Enabled=false)." });
        }

        var userEmail = User.Identity?.Name ?? "SuperAdmin";
        _logger.LogInformation("SuperAdmin {User} confirmed execution of existing product image mapping.", userEmail);

        var result = await _catalogImportService.ExecuteImageMappingAsync(userEmail, ct);
        return Json(new { success = result.Success, data = result });
    }
}

