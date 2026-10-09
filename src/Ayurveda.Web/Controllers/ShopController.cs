using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class ShopController : Controller
{
    private readonly AyurvedaDbContext _context;

    public ShopController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? categoryId, string? query, string? sort, string? concern, int page = 1)
    {
        const int pageSize = 12;
        var q = _context.Products.Include(p => p.Category).Where(p => p.IsActive);

        if (categoryId.HasValue)
        {
            q = q.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(p => p.Title.ToLower().Contains(term) || p.Brand.ToLower().Contains(term) || (p.Tags != null && p.Tags.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(concern))
        {
            var concernTerm = concern.Trim().ToLower();
            q = q.Where(p => (p.TraditionallyUsedFor != null && p.TraditionallyUsedFor.ToLower().Contains(concernTerm)) ||
                             (p.Category != null && p.Category.Name.ToLower().Contains(concernTerm)));
        }

        // Sorting
        q = sort switch
        {
            "price_asc" => q.OrderBy(p => p.Price),
            "price_desc" => q.OrderByDescending(p => p.Price),
            "rating" => q.OrderByDescending(p => p.AverageRating),
            _ => q.OrderByDescending(p => p.Id)
        };

        var totalCount = await q.CountAsync();
        var products = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductCardViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Brand = p.Brand,
                CategoryName = p.Category.Name,
                Price = p.Price,
                Mrp = p.Mrp,
                DiscountPercent = p.DiscountPercent,
                ImageUrl = p.ImageUrl,
                AverageRating = p.AverageRating,
                Stock = p.Stock,
                Badge = p.AverageRating >= 4.8 ? "Bestseller" : null
            })
            .ToListAsync();

        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .Select(c => new CategoryFilterItem
            {
                Id = c.Id,
                Name = c.Name,
                ProductCount = c.Products.Count(p => p.IsActive),
                IsSelected = categoryId.HasValue && categoryId.Value == c.Id
            })
            .ToListAsync();

        var model = new CatalogListViewModel
        {
            Products = products,
            Categories = categories,
            TotalCount = totalCount,
            PageIndex = page,
            PageSize = pageSize,
            SelectedCategoryId = categoryId,
            SelectedSort = sort ?? "popular",
            SearchQuery = query,
            Concern = concern
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Reviews)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product == null)
            return NotFound();

        var model = new ProductDetailsViewModel
        {
            Id = product.Id,
            Title = product.Title,
            Description = product.Description,
            Brand = product.Brand,
            CategoryName = product.Category.Name,
            Price = product.Price,
            Mrp = product.Mrp,
            DiscountPercent = product.DiscountPercent,
            Stock = product.Stock,
            ImageUrl = product.ImageUrl,
            AverageRating = product.AverageRating,
            ReviewCount = product.Reviews.Count,
            Sku = product.Sku,
            Tags = product.Tags,
            TraditionallyUsedFor = product.TraditionallyUsedFor,
            Ingredients = product.Ingredients,
            HowToUse = product.HowToUse,
            Precautions = product.Precautions,
            IsPrescriptionRequired = product.IsPrescriptionRequired,
            Reviews = product.Reviews.Select(r => new ProductReviewViewModel
            {
                Id = r.Id,
                UserName = r.User.FullName,
                Rating = r.Rating,
                ReviewText = r.ReviewText,
                IsVerifiedPurchase = r.IsVerifiedPurchase,
                HelpfulVotes = r.HelpfulVotes,
                CreatedAt = r.CreatedAt
            }).ToList()
        };

        return View(model);
    }
}
