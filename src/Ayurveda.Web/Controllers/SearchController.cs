using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

[Route("search")]
public class SearchController : Controller
{
    private readonly ISearchService _searchService;
    private readonly AyurvedaDbContext _context;
    private readonly IMemoryCache _cache;

    public SearchController(
        ISearchService searchService,
        AyurvedaDbContext context,
        IMemoryCache cache)
    {
        _searchService = searchService;
        _context = context;
        _cache = cache;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, [FromQuery] string scope = "public")
    {
        // 1. Sliding-window rate limiting (60 requests per minute per IP address)
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateLimitKey = $"rl_suggest_{ip}";
        if (_cache.TryGetValue(rateLimitKey, out int reqCount) && reqCount >= 60)
        {
            Response.Headers.Append("Retry-After", "10");
            return StatusCode(429, new { error = "Search rate limit exceeded. Please slow down requests." });
        }
        _cache.Set(rateLimitKey, reqCount + 1, TimeSpan.FromMinutes(1));

        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(new SearchSuggestionResultDto());
        }

        var result = await _searchService.GetSuggestionsAsync(q.Trim(), scope);

        // Include latency header for performance auditing (target p95 < 150ms)
        Response.Headers.Append("X-Search-Latency-Ms", result.LatencyMs.ToString());

        return Json(result);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q,
        [FromQuery] int? categoryId,
        [FromQuery] string? brand,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] double? minRating,
        [FromQuery] string? sort = "relevance",
        [FromQuery] int page = 1)
    {
        var sw = Stopwatch.StartNew();
        const int pageSize = 12;
        var cleanQuery = q?.Trim() ?? string.Empty;

        // Fetch suggestions to extract DidYouMean, synonyms, matching herbs, doctors
        var suggestionResult = await _searchService.GetSuggestionsAsync(cleanQuery, "public");

        var productsQuery = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .Where(p => p.IsActive);

        // Apply text search
        if (!string.IsNullOrWhiteSpace(cleanQuery))
        {
            var term = cleanQuery.ToLower();
            string? synonymCanonical = null;

            if (!string.IsNullOrWhiteSpace(suggestionResult.SynonymApplied))
            {
                // Format is: "CanonicalTerm (CategoryName)"
                var parts = suggestionResult.SynonymApplied.Split('(');
                if (parts.Length > 0)
                {
                    synonymCanonical = parts[0].Trim().ToLower();
                }
            }

            productsQuery = productsQuery.Where(p =>
                p.Title.ToLower().Contains(term) ||
                p.Brand.ToLower().Contains(term) ||
                (p.Tags != null && p.Tags.ToLower().Contains(term)) ||
                (p.TraditionallyUsedFor != null && p.TraditionallyUsedFor.ToLower().Contains(term)) ||
                p.Category.Name.ToLower().Contains(term) ||
                (synonymCanonical != null && (
                    p.Title.ToLower().Contains(synonymCanonical) ||
                    (p.Tags != null && p.Tags.ToLower().Contains(synonymCanonical)) ||
                    (p.TraditionallyUsedFor != null && p.TraditionallyUsedFor.ToLower().Contains(synonymCanonical)) ||
                    p.Category.Name.ToLower().Contains(synonymCanonical)
                )));
        }

        // Category filter
        if (categoryId.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
        }

        // Brand filter
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var trimmedBrand = brand.Trim();
            productsQuery = productsQuery.Where(p => p.Brand == trimmedBrand);
        }

        // Price filter
        if (minPrice.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Price >= minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Price <= maxPrice.Value);
        }

        // Minimum rating filter
        if (minRating.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.AverageRating >= minRating.Value);
        }

        // Sorting
        productsQuery = sort switch
        {
            "price_asc" => productsQuery.OrderBy(p => p.Price),
            "price_desc" => productsQuery.OrderByDescending(p => p.Price),
            "rating" => productsQuery.OrderByDescending(p => p.AverageRating),
            "newest" => productsQuery.OrderByDescending(p => p.Id),
            _ => productsQuery.OrderByDescending(p => p.AverageRating).ThenByDescending(p => p.Id) // relevance
        };

        var totalCount = await productsQuery.CountAsync();
        var pagedItems = await productsQuery
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
                Badge = p.AverageRating >= 4.9 ? "Bestseller" : (p.DiscountPercent >= 20 ? $"{p.DiscountPercent}% Off" : null),
                Sku = p.Sku,
                TraditionallyUsedFor = p.TraditionallyUsedFor
            })
            .ToListAsync();

        // Faceted categories
        var allCategories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => new CategoryFilterItem
            {
                Id = c.Id,
                Name = c.Name,
                ProductCount = c.Products.Count(p => p.IsActive),
                IsSelected = categoryId.HasValue && categoryId.Value == c.Id
            })
            .ToListAsync();

        // Faceted brands
        var allBrands = await _context.Products
            .Where(p => p.IsActive)
            .GroupBy(p => p.Brand)
            .Select(g => new BrandFilterItem
            {
                Name = g.Key,
                ProductCount = g.Count(),
                IsSelected = brand != null && brand.Equals(g.Key, StringComparison.OrdinalIgnoreCase)
            })
            .OrderBy(b => b.Name)
            .ToListAsync();

        // Active filter chips
        var activeChips = new List<ActiveFilterChip>();
        if (!string.IsNullOrWhiteSpace(cleanQuery))
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "q",
                Label = $"Query: \"{cleanQuery}\"",
                RemoveUrl = BuildUrlWithoutParam("q", cleanQuery, categoryId, brand, minPrice, maxPrice, minRating, sort)
            });
        }
        if (categoryId.HasValue)
        {
            var catName = allCategories.FirstOrDefault(c => c.Id == categoryId.Value)?.Name ?? "Category";
            activeChips.Add(new ActiveFilterChip
            {
                Key = "categoryId",
                Label = catName,
                RemoveUrl = BuildUrlWithoutParam("categoryId", cleanQuery, categoryId, brand, minPrice, maxPrice, minRating, sort)
            });
        }
        if (!string.IsNullOrWhiteSpace(brand))
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "brand",
                Label = brand,
                RemoveUrl = BuildUrlWithoutParam("brand", cleanQuery, categoryId, brand, minPrice, maxPrice, minRating, sort)
            });
        }
        if (minPrice.HasValue || maxPrice.HasValue)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "price",
                Label = $"₹{minPrice ?? 0} - ₹{maxPrice ?? 5000}",
                RemoveUrl = BuildUrlWithoutParam("price", cleanQuery, categoryId, brand, minPrice, maxPrice, minRating, sort)
            });
        }
        if (minRating.HasValue)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "minRating",
                Label = $"{minRating.Value}★ & above",
                RemoveUrl = BuildUrlWithoutParam("minRating", cleanQuery, categoryId, brand, minPrice, maxPrice, minRating, sort)
            });
        }

        sw.Stop();

        var viewModel = new SearchResultsViewModel
        {
            Query = cleanQuery,
            DidYouMean = suggestionResult.DidYouMean,
            SynonymApplied = suggestionResult.SynonymApplied,
            LatencyMs = sw.ElapsedMilliseconds,
            MatchingHerbs = suggestionResult.Herbs,
            MatchingDoctors = suggestionResult.Doctors,
            TrendingSearches = suggestionResult.TrendingSearches,
            Catalog = new CatalogListViewModel
            {
                Products = pagedItems,
                Categories = allCategories,
                Brands = allBrands,
                ActiveChips = activeChips,
                TotalCount = totalCount,
                PageIndex = page,
                PageSize = pageSize,
                SelectedCategoryId = categoryId,
                SelectedBrand = brand,
                SelectedSort = sort,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MinRating = minRating,
                SearchQuery = cleanQuery
            }
        };

        return View(viewModel);
    }

    private static string BuildUrlWithoutParam(
        string removeKey,
        string? q,
        int? categoryId,
        string? brand,
        decimal? minPrice,
        decimal? maxPrice,
        double? minRating,
        string? sort)
    {
        var queryParams = new List<string>();

        if (removeKey != "q" && !string.IsNullOrWhiteSpace(q))
            queryParams.Add($"q={Uri.EscapeDataString(q)}");

        if (removeKey != "categoryId" && categoryId.HasValue)
            queryParams.Add($"categoryId={categoryId.Value}");

        if (removeKey != "brand" && !string.IsNullOrWhiteSpace(brand))
            queryParams.Add($"brand={Uri.EscapeDataString(brand)}");

        if (removeKey != "price")
        {
            if (minPrice.HasValue) queryParams.Add($"minPrice={minPrice.Value}");
            if (maxPrice.HasValue) queryParams.Add($"maxPrice={maxPrice.Value}");
        }

        if (removeKey != "minRating" && minRating.HasValue)
            queryParams.Add($"minRating={minRating.Value}");

        if (!string.IsNullOrWhiteSpace(sort) && sort != "relevance")
            queryParams.Add($"sort={Uri.EscapeDataString(sort)}");

        return queryParams.Count > 0 ? $"/search?{string.Join("&", queryParams)}" : "/search";
    }
}
