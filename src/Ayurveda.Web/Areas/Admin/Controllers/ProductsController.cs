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

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookieScheme", Policy = "RequireCatalogManagement")]
public class ProductsController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IFileStorageService _fileStorage;
    private readonly ICacheBuster _cacheBuster;

    public ProductsController(AyurvedaDbContext context, IAuditService auditService, IFileStorageService fileStorage, ICacheBuster cacheBuster)
    {
        _context = context;
        _auditService = auditService;
        _fileStorage = fileStorage;
        _cacheBuster = cacheBuster;
    }

    // -------------------------------------------------------------------------
    // 1. CATALOGUE DIRECTORY & STOCK AUDIT
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Index(int? categoryId, bool? lowStock, bool? outOfStock, string? search, int page = 1)
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

        if (outOfStock == true)
        {
            q = q.Where(p => p.Stock <= 0);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(p => p.Title.Contains(search) || (p.Sku != null && p.Sku.Contains(search)) || (p.Brand != null && p.Brand.Contains(search)));
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
            OutOfStockOnly = outOfStock,
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
        ViewBag.Subcategories = await _context.Subcategories.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        return View(new AdminProductEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminProductEditViewModel model, IFormFile? imageFile, IFormFile[]? galleryFiles)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };

        if (imageFile != null && imageFile.Length > 0)
        {
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("imageFile", "Allowed file types: JPG, PNG, WEBP, SVG.");
            }
            else if (imageFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("imageFile", "Main image size cannot exceed 5MB.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            ViewBag.Subcategories = await _context.Subcategories.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            return View(model);
        }

        var category = await _context.Categories.FindAsync(model.CategoryId);
        string categoryName = category?.Name ?? "Ayurvedic Medicine";

        string imageUrl = model.ImageUrl ?? string.Empty;
        if (imageFile != null && imageFile.Length > 0)
        {
            using var stream = imageFile.OpenReadStream();
            imageUrl = await _fileStorage.SaveFileAsync(stream, imageFile.FileName, imageFile.ContentType);
        }
        else if (string.IsNullOrWhiteSpace(imageUrl) || imageUrl.Contains("placeholder.svg"))
        {
            // Generate a unique, aesthetically rich SVG botanical placeholder with the product title
            imageUrl = await GenerateProductPlaceholderAsync(model.Title, categoryName);
        }

        var product = new Product
        {
            Title = model.Title.Trim(),
            Sku = string.IsNullOrWhiteSpace(model.Sku) ? $"TGA-{Guid.NewGuid().ToString("N")[..6].ToUpper()}" : model.Sku.Trim().ToUpper(),
            Mrp = model.Mrp > 0 ? model.Mrp : model.Price * 1.25m,
            Price = model.Price,
            Stock = model.StockQuantity,
            CategoryId = model.CategoryId,
            SubcategoryId = model.SubcategoryId > 0 ? model.SubcategoryId : null,
            Brand = string.IsNullOrWhiteSpace(model.Brand) ? "The Great Ayurveda Labs" : model.Brand.Trim(),
            Description = model.Description.Trim(),
            Ingredients = model.KeyIngredients?.Trim(),
            HowToUse = model.HowToUse?.Trim(),
            ImageUrl = imageUrl,
            IsActive = model.IsActive,
            AverageRating = 5.0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Save multiple gallery images if uploaded
        if (galleryFiles != null && galleryFiles.Length > 0)
        {
            int order = 1;
            foreach (var gf in galleryFiles)
            {
                if (gf.Length > 0 && gf.Length <= 5 * 1024 * 1024)
                {
                    var gExt = Path.GetExtension(gf.FileName).ToLowerInvariant();
                    if (allowedExtensions.Contains(gExt))
                    {
                        using var gStream = gf.OpenReadStream();
                        var gUrl = await _fileStorage.SaveFileAsync(gStream, gf.FileName, gf.ContentType);
                        _context.ProductImages.Add(new ProductImage
                        {
                            ProductId = product.Id,
                            ImageUrl = gUrl,
                            DisplayOrder = order++,
                            IsPrimary = false
                        });
                    }
                }
            }
            await _context.SaveChangesAsync();
        }

        await _auditService.LogAsync("ProductCreated", "Product", product.Id.ToString(), $"Created new Ayurvedic product '{product.Title}' (SKU: {product.Sku})", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
        _cacheBuster.InvalidateProduct(product.Id);

        TempData["SuccessMessage"] = $"Formulation '{product.Title}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 3. EDIT PRODUCT & RE-STOCK
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        ViewBag.Subcategories = await _context.Subcategories.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

        var model = new AdminProductEditViewModel
        {
            Id = product.Id,
            Title = product.Title,
            Sku = product.Sku ?? string.Empty,
            Mrp = product.Mrp,
            Price = product.Price,
            StockQuantity = product.Stock,
            CategoryId = product.CategoryId,
            SubcategoryId = product.SubcategoryId,
            Brand = product.Brand ?? "The Great Ayurveda Labs",
            Description = product.Description,
            KeyIngredients = product.Ingredients,
            HowToUse = product.HowToUse,
            ImageUrl = product.ImageUrl,
            IsActive = product.IsActive,
            GalleryImageUrls = product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminProductEditViewModel model, IFormFile? imageFile, IFormFile[]? galleryFiles)
    {
        var product = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == model.Id);
        if (product == null) return NotFound();

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };

        if (imageFile != null && imageFile.Length > 0)
        {
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (allowedExtensions.Contains(ext) && imageFile.Length <= 5 * 1024 * 1024)
            {
                using var stream = imageFile.OpenReadStream();
                product.ImageUrl = await _fileStorage.SaveFileAsync(stream, imageFile.FileName, imageFile.ContentType);
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrl) && !model.ImageUrl.Contains("placeholder"))
        {
            product.ImageUrl = model.ImageUrl;
        }

        product.Title = model.Title.Trim();
        product.Sku = model.Sku?.Trim();
        product.Mrp = model.Mrp;
        product.Price = model.Price;
        product.Stock = model.StockQuantity;
        product.CategoryId = model.CategoryId;
        product.SubcategoryId = model.SubcategoryId > 0 ? model.SubcategoryId : null;
        product.Brand = model.Brand?.Trim() ?? "The Great Ayurveda Labs";
        product.Description = model.Description.Trim();
        product.Ingredients = model.KeyIngredients?.Trim();
        product.HowToUse = model.HowToUse?.Trim();
        product.IsActive = model.IsActive;

        // Append new gallery images
        if (galleryFiles != null && galleryFiles.Length > 0)
        {
            int order = product.Images.Count + 1;
            foreach (var gf in galleryFiles)
            {
                if (gf.Length > 0 && gf.Length <= 5 * 1024 * 1024)
                {
                    var gExt = Path.GetExtension(gf.FileName).ToLowerInvariant();
                    if (allowedExtensions.Contains(gExt))
                    {
                        using var gStream = gf.OpenReadStream();
                        var gUrl = await _fileStorage.SaveFileAsync(gStream, gf.FileName, gf.ContentType);
                        _context.ProductImages.Add(new ProductImage
                        {
                            ProductId = product.Id,
                            ImageUrl = gUrl,
                            DisplayOrder = order++,
                            IsPrimary = false
                        });
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("ProductUpdated", "Product", product.Id.ToString(), $"Updated product '{product.Title}', Price: ₹{product.Price}, Stock: {product.Stock}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
        _cacheBuster.InvalidateProduct(product.Id);

        TempData["SuccessMessage"] = $"Formulation '{product.Title}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 4. QUICK STOCK UPDATE
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickStockUpdate(int id, int stock)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            product.Stock = Math.Max(0, stock);
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("ProductStockUpdated", "Product", id.ToString(), $"Quick stock update for '{product.Title}' to {product.Stock}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            _cacheBuster.InvalidateProduct(product.Id);
            TempData["SuccessMessage"] = $"Stock for '{product.Title}' updated to {product.Stock} units.";
        }
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 5. DELETE PRODUCT (SAFE DELETE: ARCHIVE IF ORDERED, DELETE IF CLEAN)
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (product != null)
        {
            var title = product.Title;
            var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
            if (hasOrders)
            {
                product.IsActive = false;
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("ProductArchived", "Product", id.ToString(), $"Archived ordered product '{title}' (IsActive=false)", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
                TempData["SuccessMessage"] = $"Formulation '{title}' is referenced by customer orders and was archived (hidden from shop, order history preserved).";
            }
            else
            {
                if (product.Images.Any())
                {
                    _context.ProductImages.RemoveRange(product.Images);
                }
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("ProductDeleted", "Product", id.ToString(), $"Deleted formulation '{title}'", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
                _cacheBuster.InvalidateProduct(id);
                TempData["SuccessMessage"] = $"Formulation '{title}' was deleted successfully.";
            }
            _cacheBuster.InvalidateProduct(id);
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateProductPlaceholderAsync(string title, string categoryName)
    {
        var colors = new[] { 
            ("#1F5E3B", "#E8F5E9"), 
            ("#0C6258", "#E0F2F1"), 
            ("#936015", "#FFF8E1"), 
            ("#2C7E50", "#E8F8F0"),
            ("#1A535C", "#F0FDF4") 
        };
        var palette = colors[Math.Abs(title.GetHashCode()) % colors.Length];
        var primaryColor = palette.Item1;
        var bgColor = palette.Item2;
        var safeTitle = System.Net.WebUtility.HtmlEncode(title);
        var safeCat = System.Net.WebUtility.HtmlEncode(categoryName);
        
        var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 600" width="600" height="600"><defs><linearGradient id="bgGrad" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="{bgColor}"/><stop offset="100%" stop-color="#FFFFFF"/></linearGradient></defs><rect width="600" height="600" rx="32" fill="url(#bgGrad)"/><circle cx="300" cy="240" r="130" fill="{primaryColor}" opacity="0.12"/><circle cx="300" cy="240" r="105" fill="{primaryColor}" opacity="0.2"/><path d="M300 160 C330 200 350 250 300 300 C250 250 270 200 300 160 Z" fill="{primaryColor}"/><path d="M300 170 L300 290" stroke="#FFFFFF" stroke-width="3" stroke-linecap="round"/><text x="300" y="400" font-family="'Plus Jakarta Sans', system-ui, sans-serif" font-size="28" font-weight="700" fill="{primaryColor}" text-anchor="middle">{safeTitle}</text><text x="300" y="440" font-family="'Plus Jakarta Sans', system-ui, sans-serif" font-size="16" font-weight="600" fill="#6B7280" text-anchor="middle">{safeCat} • Shastriya</text><text x="300" y="480" font-family="system-ui, sans-serif" font-size="14" fill="#9CA3AF" text-anchor="middle">The Great Ayurveda • Classical Formulation</text></svg>""";
        
        var bytes = System.Text.Encoding.UTF8.GetBytes(svg);
        using var ms = new MemoryStream(bytes);
        return await _fileStorage.SaveFileAsync(ms, $"prod_placeholder_{Guid.NewGuid():N}.svg", "image/svg+xml");
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
            _cacheBuster.InvalidateProduct(id);
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
        _cacheBuster.InvalidateProduct();

        TempData["SuccessMessage"] = $"Bulk inventory & pricing updated for {updatedCount} formulations.";
        return RedirectToAction(nameof(Index));
    }
}
