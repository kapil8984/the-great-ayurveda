using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookieScheme", Policy = "RequireCatalogManagement")]
public class YogaController : Controller
{
    private readonly AyurvedaDbContext _context;

    public YogaController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var yogas = await _context.Yogas
            .Include(y => y.Category)
            .OrderBy(y => y.YogaName)
            .ToListAsync();

        return View(yogas);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var yoga = await _context.Yogas
            .Include(y => y.Category)
            .FirstOrDefaultAsync(y => y.Id == id);

        if (yoga == null)
            return NotFound();

        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(yoga);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Yoga model)
    {
        if (id != model.Id)
            return BadRequest();

        var existing = await _context.Yogas.FindAsync(id);
        if (existing == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(model.YogaName))
        {
            ModelState.AddModelError("YogaName", "Yoga posture name is required.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        existing.YogaName = model.YogaName;
        existing.Description = model.Description;
        existing.Instructions = model.Instructions;
        existing.ImageUrl = model.ImageUrl;
        existing.VideoUrl = model.VideoUrl;
        existing.CategoryId = model.CategoryId;
        existing.BenefitsJson = model.BenefitsJson;
        existing.AverageRating = model.AverageRating;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Yoga posture '{existing.YogaName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
