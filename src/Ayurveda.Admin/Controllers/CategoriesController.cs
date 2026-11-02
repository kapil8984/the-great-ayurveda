using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Policy = "RequireCatalogManagement")]
public class CategoriesController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditService _auditService;

    public CategoriesController(AyurvedaDbContext context, IFileStorageService fileStorage, IAuditService auditService)
    {
        _context = context;
        _fileStorage = fileStorage;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, bool? active, int page = 1)
    {
        const int pageSize = 15;
        var query = _context.Categories
            .Include(c => c.Products)
            .Include(c => c.Subcategories)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(c => c.Name.Contains(search) || (c.Description != null && c.Description.Contains(search)));
        }

        if (active.HasValue)
        {
            query = query.Where(c => c.IsActive == active.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Active = active;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
        ViewBag.TotalCount = total;

        return View(items);
    }

    public IActionResult Create()
    {
        return View(new Category { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category model, IFormFile? imageFile)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Category name is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string imageUrl = model.ImageUrl ?? string.Empty;
        if (imageFile != null && imageFile.Length > 0)
        {
            using var stream = imageFile.OpenReadStream();
            imageUrl = await _fileStorage.SaveFileAsync(stream, imageFile.FileName, imageFile.ContentType);
        }
        else if (string.IsNullOrWhiteSpace(imageUrl))
        {
            var slug = model.Name.ToLowerInvariant().Replace(" ", "");
            if (slug.Contains("digestion") || slug.Contains("gut")) imageUrl = "/images/categories/digestion.svg";
            else if (slug.Contains("skin")) imageUrl = "/images/categories/skin.svg";
            else if (slug.Contains("hair")) imageUrl = "/images/categories/hair.svg";
            else if (slug.Contains("immunity")) imageUrl = "/images/categories/immunity.svg";
            else if (slug.Contains("joint")) imageUrl = "/images/categories/joints.svg";
            else if (slug.Contains("sleep") || slug.Contains("stress")) imageUrl = "/images/categories/sleep.svg";
            else if (slug.Contains("women")) imageUrl = "/images/categories/womens.svg";
            else if (slug.Contains("food")) imageUrl = "/images/categories/foods.svg";
            else if (slug.Contains("yoga")) imageUrl = "/images/categories/yoga.svg";
            else imageUrl = "/images/categories/herbs.svg";
        }

        model.ImageUrl = imageUrl;
        _context.Categories.Add(model);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("CategoryCreated", "Category", model.Id.ToString(), $"Created category '{model.Name}'", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Category '{model.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category model, IFormFile? imageFile)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Category name is required.");
            return View(model);
        }

        category.Name = model.Name.Trim();
        category.Description = model.Description?.Trim();
        category.IsActive = model.IsActive;

        if (imageFile != null && imageFile.Length > 0)
        {
            using var stream = imageFile.OpenReadStream();
            category.ImageUrl = await _fileStorage.SaveFileAsync(stream, imageFile.FileName, imageFile.ContentType);
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            category.ImageUrl = model.ImageUrl;
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("CategoryUpdated", "Category", category.Id.ToString(), $"Updated category '{category.Name}'", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Category '{category.Name}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category != null)
        {
            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("CategoryToggled", "Category", id.ToString(), $"Toggled category '{category.Name}' to active={category.IsActive}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"Category '{category.Name}' is now {(category.IsActive ? "Active" : "Disabled")}.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null) return NotFound();

        if (category.Products.Any())
        {
            TempData["ErrorMessage"] = $"Cannot delete category '{category.Name}' because it has {category.Products.Count} products linked. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("CategoryDeleted", "Category", id.ToString(), $"Deleted category '{category.Name}'", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Category '{category.Name}' deleted.";
        return RedirectToAction(nameof(Index));
    }
}
