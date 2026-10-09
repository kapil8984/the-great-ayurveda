using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Roles = "SuperAdmin,CatalogManager,Admin")]
public class ProductsController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;

    public ProductsController(AyurvedaDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // -------------------------------------------------------------------------
    // 1. CATALOGUE DIRECTORY & STOCK AUDIT
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Index(int? categoryId, bool? lowStock, string? search, int page = 1)
    {
        int pageSize = 15;
        var q = _context.Products.Include(p => p.Category).AsQueryable();

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            q = q.Where(p => p.CategoryId == categoryId.Value);
        }

        if (lowStock == true)
        {
            q = q.Where(p => p.Stock < 10 && p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(p => p.Title.Contains(search) || p.Sku.Contains(search) || (p.Brand != null && p.Brand.Contains(search)));
        }

        var total = await q.CountAsync();
        var products = await q
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

        var model = new AdminProductListViewModel
        {
            Products = products,
            Categories = categories,
            SelectedCategoryId = categoryId,
            LowStockOnly = lowStock,
            SearchQuery = search,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            TotalCount = total
        };

        return View(model);
    }

    // -------------------------------------------------------------------------
    // 2. CREATE PRODUCT
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        return View(new AdminProductEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminProductEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        var product = new Product
        {
            Title = model.Title,
            Sku = string.IsNullOrWhiteSpace(model.Sku) ? $"TGA-{Guid.NewGuid().ToString("N")[..6].ToUpper()}" : model.Sku.Trim().ToUpper(),
            Mrp = model.Mrp > 0 ? model.Mrp : model.Price * 1.2m,
            Price = model.Price,
            Stock = model.StockQuantity,
            CategoryId = model.CategoryId,
            Brand = model.Brand ?? "The Great Ayurveda Labs",
            Description = model.Description,
            Ingredients = model.KeyIngredients,
            HowToUse = model.HowToUse,
            ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? "/images/products/placeholder.svg" : model.ImageUrl,
            IsActive = model.IsActive,
            AverageRating = 5.0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("ProductCreated", "Product", product.Id.ToString(), $"Created new Ayurvedic product '{product.Title}' (SKU: {product.Sku})", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Formulation '{product.Title}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 3. EDIT PRODUCT & RE-STOCK
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

        var model = new AdminProductEditViewModel
        {
            Id = product.Id,
            Title = product.Title,
            Sku = product.Sku,
            Mrp = product.Mrp,
            Price = product.Price,
            StockQuantity = product.Stock,
            CategoryId = product.CategoryId,
            Brand = product.Brand ?? "The Great Ayurveda Labs",
            Description = product.Description,
            KeyIngredients = product.Ingredients,
            HowToUse = product.HowToUse,
            ImageUrl = product.ImageUrl,
            IsActive = product.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminProductEditViewModel model)
    {
        var product = await _context.Products.FindAsync(model.Id);
        if (product == null) return NotFound();

        product.Title = model.Title;
        product.Sku = model.Sku;
        product.Mrp = model.Mrp;
        product.Price = model.Price;
        product.Stock = model.StockQuantity;
        product.CategoryId = model.CategoryId;
        product.Brand = model.Brand;
        product.Description = model.Description;
        product.Ingredients = model.KeyIngredients;
        product.HowToUse = model.HowToUse;
        product.ImageUrl = model.ImageUrl;
        product.IsActive = model.IsActive;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("ProductUpdated", "Product", product.Id.ToString(), $"Updated product '{product.Title}', Price: ₹{product.Price}, Stock: {product.Stock}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Formulation '{product.Title}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 4. PUBLISH / UNPUBLISH TOGGLE
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            product.IsActive = !product.IsActive;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("ProductStatusToggled", "Product", id.ToString(), $"Changed active status to {product.IsActive} for '{product.Title}'", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = $"Product '{product.Title}' is now {(product.IsActive ? "Published & Live" : "Unpublished")}.";
        }
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 5. BULK CSV EXPORT & IMPORT
    // -------------------------------------------------------------------------
    public async Task<IActionResult> ExportCsv()
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Id,Title,SKU,Category,Brand,Price,MRP,StockQuantity,IsActive,CreatedAt");

        foreach (var p in products)
        {
            var title = $"\"{p.Title.Replace("\"", "\"\"")}\"";
            var category = $"\"{p.Category?.Name.Replace("\"", "\"\"") ?? "General"}\"";
            var brand = $"\"{p.Brand?.Replace("\"", "\"\"") ?? "TGA"}\"";
            sb.AppendLine($"{p.Id},{title},{p.Sku},{category},{brand},{p.Price},{p.Mrp},{p.Stock},{p.IsActive},{p.CreatedAt:yyyy-MM-dd}");
        }

        await _auditService.LogAsync("CatalogueExportCsv", "Product", "All", $"Exported {products.Count} catalogue items as CSV", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"tga_catalogue_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCsv(IFormFile csvFile)
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a valid CSV file.";
            return RedirectToAction(nameof(Index));
        }

        int updatedCount = 0;
        using (var reader = new StreamReader(csvFile.OpenReadStream()))
        {
            string? line = await reader.ReadLineAsync(); // Skip header
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length >= 8)
                {
                    if (int.TryParse(parts[0], out int id))
                    {
                        var prod = await _context.Products.FindAsync(id);
                        if (prod != null)
                        {
                            if (decimal.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                                prod.Price = price;
                            if (int.TryParse(parts[7], out int stock))
                                prod.Stock = stock;

                            updatedCount++;
                        }
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("CatalogueImportCsv", "Product", "Bulk", $"Imported CSV update for {updatedCount} products", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Bulk inventory & pricing updated for {updatedCount} formulations.";
        return RedirectToAction(nameof(Index));
    }
}
