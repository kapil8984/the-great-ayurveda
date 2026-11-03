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
    private readonly IConfiguration _config;

    public SearchController(
        ISearchService searchService,
        AyurvedaDbContext context,
        IMemoryCache cache,
        IConfiguration config)
    {
        _searchService = searchService;
        _context = context;
        _cache = cache;
        _config = config;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, [FromQuery] string scope = "public")
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(new SearchSuggestionResultDto());
        }

        var result = await _searchService.GetSuggestionsAsync(q.Trim(), scope);

        // Include latency header for performance auditing (target p95 < 150ms)
        Response.Headers.Append("X-Search-Latency-Ms", result.LatencyMs.ToString());

        return Json(result);
    }

    [HttpGet("placeholders")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Placeholders()
    {
        var terms = _cache.GetOrCreate("Search_Placeholder_Terms", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2);
            var topProducts = _context.Products
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.AverageRating)
                .Take(12)
                .Select(p => p.Title)
                .ToList();

            var topConcerns = new[]
            {
                "Digestion & Gut Agni",
                "Stress, Anxiety & Sleep",
                "Immunity & Rasayana",
                "Joint & Muscle Mobility",
                "Hair Vitality & Scalp",
                "Skin Glow & Detox",
                "Healthy Metabolism",
                "Women's Hormonal Care"
            };

            var list = new List<string>();
            var max = Math.Max(topProducts.Count, topConcerns.Length);
            for (int i = 0; i < max && list.Count < 20; i++)
            {
                if (i < topProducts.Count) list.Add(topProducts[i]);
                if (i < topConcerns.Length) list.Add(topConcerns[i]);
            }
            return list.Distinct().Take(20).ToList();
        });

        return Json(terms);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q,
        [FromQuery] int? categoryId,
        [FromQuery] int? subcategoryId,
        [FromQuery] string? brand,
        [FromQuery] string? concern,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] double? minRating,
        [FromQuery] int? minDiscount,
        [FromQuery] bool inStockOnly = false,
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

        // Text search
        if (!string.IsNullOrWhiteSpace(cleanQuery))
        {
            var term = cleanQuery.ToLower();
            string? synonymCanonical = null;

            if (!string.IsNullOrWhiteSpace(suggestionResult.SynonymApplied))
            {
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

        // Category & Subcategory filters
        if (categoryId.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
        }
        if (subcategoryId.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.SubcategoryId == subcategoryId.Value);
        }

        // Brand filter
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var trimmedBrand = brand.Trim();
            productsQuery = productsQuery.Where(p => p.Brand == trimmedBrand);
        }

        // Concern filter
        if (!string.IsNullOrWhiteSpace(concern))
        {
            var concernLower = concern.Trim().ToLower();
            productsQuery = productsQuery.Where(p =>
                (p.TraditionallyUsedFor != null && p.TraditionallyUsedFor.ToLower().Contains(concernLower)) ||
                (p.Tags != null && p.Tags.ToLower().Contains(concernLower)));
        }

        // Price range
        if (minPrice.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Price >= minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Price <= maxPrice.Value);
        }

        // Rating filter
        if (minRating.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.AverageRating >= minRating.Value);
        }

        // Discount filter (10%, 20%, 30%+)
        if (minDiscount.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.DiscountPercent >= minDiscount.Value);
        }

        // In-stock only
        if (inStockOnly)
        {
            productsQuery = productsQuery.Where(p => p.Stock > 0);
        }

        // Sorting
        productsQuery = sort switch
        {
            "price_asc" => productsQuery.OrderBy(p => p.Price),
            "price_desc" => productsQuery.OrderByDescending(p => p.Price),
            "rating" => productsQuery.OrderByDescending(p => p.AverageRating),
            "newest" => productsQuery.OrderByDescending(p => p.Id),
            "discount" => productsQuery.OrderByDescending(p => p.DiscountPercent),
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

        // Cached facet counts (5 min TTL)
        var cachedFacets = await _cache.GetOrCreateAsync("Search_Facet_Counts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => new CategoryFilterItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    ProductCount = c.Products.Count(p => p.IsActive)
                })
                .ToListAsync();

            var subcategories = await _context.Subcategories
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new SubcategoryFilterItem
                {
                    Id = s.Id,
                    CategoryId = s.CategoryId,
                    Name = s.Name,
                    ProductCount = s.Products.Count(p => p.IsActive)
                })
                .ToListAsync();

            var brands = await _context.Products
                .Where(p => p.IsActive)
                .GroupBy(p => p.Brand)
                .Select(g => new BrandFilterItem
                {
                    Name = g.Key,
                    ProductCount = g.Count()
                })
                .OrderBy(b => b.Name)
                .ToListAsync();

            return (categories, subcategories, brands);
        });

        var (allCategories, allSubcategories, allBrands) = cachedFacets;

        // Mark selected states
        var categoriesWithSelected = allCategories.Select(c => new CategoryFilterItem
        {
            Id = c.Id,
            Name = c.Name,
            ProductCount = c.ProductCount,
            IsSelected = categoryId.HasValue && categoryId.Value == c.Id
        }).ToList();

        var subcategoriesWithSelected = allSubcategories
            .Where(s => !categoryId.HasValue || s.CategoryId == categoryId.Value)
            .Select(s => new SubcategoryFilterItem
            {
                Id = s.Id,
                CategoryId = s.CategoryId,
                Name = s.Name,
                ProductCount = s.ProductCount,
                IsSelected = subcategoryId.HasValue && subcategoryId.Value == s.Id
            }).ToList();

        var brandsWithSelected = allBrands.Select(b => new BrandFilterItem
        {
            Name = b.Name,
            ProductCount = b.ProductCount,
            IsSelected = brand != null && brand.Equals(b.Name, StringComparison.OrdinalIgnoreCase)
        }).ToList();

        // Available concerns
        var availableConcerns = new List<string>
        {
            "Digestion", "Stress", "Immunity", "Joints", "Hair", "Skin", "Metabolism", "Womens"
        };

        // Active filter chips
        var activeChips = new List<ActiveFilterChip>();
        if (!string.IsNullOrWhiteSpace(cleanQuery))
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "q",
                Label = $"Query: \"{cleanQuery}\"",
                RemoveUrl = BuildUrlWithoutParam("q", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
            });
        }
        if (categoryId.HasValue)
        {
            var catName = categoriesWithSelected.FirstOrDefault(c => c.Id == categoryId.Value)?.Name ?? "Category";
            activeChips.Add(new ActiveFilterChip
            {
                Key = "categoryId",
                Label = catName,
                RemoveUrl = BuildUrlWithoutParam("categoryId", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
            });
        }
        if (subcategoryId.HasValue)
        {
            var subName = subcategoriesWithSelected.FirstOrDefault(s => s.Id == subcategoryId.Value)?.Name ?? "Subcategory";
            activeChips.Add(new ActiveFilterChip
            {
                Key = "subcategoryId",
                Label = subName,
                RemoveUrl = BuildUrlWithoutParam("subcategoryId", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
            });
        }
        if (!string.IsNullOrWhiteSpace(brand))
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "brand",
                Label = brand,
                RemoveUrl = BuildUrlWithoutParam("brand", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
            });
        }
        if (!string.IsNullOrWhiteSpace(concern))
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "concern",
                Label = $"Concern: {concern}",
                RemoveUrl = BuildUrlWithoutParam("concern", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
            });
        }
        if (minPrice.HasValue || maxPrice.HasValue)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "price",
                Label = $"₹{minPrice ?? 0} - ₹{maxPrice ?? 5000}",
                RemoveUrl = BuildUrlWithoutParam("price", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
            });
        }
        if (minRating.HasValue)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "minRating",
                Label = $"{minRating.Value}★ & above",
                RemoveUrl = BuildUrlWithoutParam("minRating", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
            });
        }
        if (minDiscount.HasValue)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "minDiscount",
                Label = $"{minDiscount.Value}% or more off",
                RemoveUrl = BuildUrlWithoutParam("minDiscount", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
            });
        }
        if (inStockOnly)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "inStockOnly",
                Label = "In Stock Only",
                RemoveUrl = BuildUrlWithoutParam("inStockOnly", cleanQuery, categoryId, subcategoryId, brand, concern, minPrice, maxPrice, minRating, minDiscount, inStockOnly, sort)
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
                Categories = categoriesWithSelected,
                Subcategories = subcategoriesWithSelected,
                Brands = brandsWithSelected,
                AvailableConcerns = availableConcerns,
                ActiveChips = activeChips,
                TotalCount = totalCount,
                PageIndex = page,
                PageSize = pageSize,
                SelectedCategoryId = categoryId,
                SelectedSubcategoryId = subcategoryId,
                SelectedBrand = brand,
                SelectedSort = sort,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MinRating = minRating,
                MinDiscount = minDiscount,
                InStockOnly = inStockOnly,
                Concern = concern,
                SearchQuery = cleanQuery
            }
        };

        ViewBag.AdShopInline = await _context.Banners
            .AsNoTracking()
            .Where(b => b.IsActive && b.Placement == "ad_shop_inline")
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();

        ViewBag.AdShopSide = await _context.Banners
            .AsNoTracking()
            .Where(b => b.IsActive && b.Placement == "ad_shop_side")
            .OrderBy(b => b.DisplayOrder)
            .FirstOrDefaultAsync();

        return View(viewModel);
    }

    private static string BuildUrlWithoutParam(
        string removeKey,
        string? q,
        int? categoryId,
        int? subcategoryId,
        string? brand,
        string? concern,
        decimal? minPrice,
        decimal? maxPrice,
        double? minRating,
        int? minDiscount,
        bool inStockOnly,
        string? sort)
    {
        var queryParams = new List<string>();

        if (removeKey != "q" && !string.IsNullOrWhiteSpace(q))
            queryParams.Add($"q={Uri.EscapeDataString(q)}");

        if (removeKey != "categoryId" && categoryId.HasValue)
            queryParams.Add($"categoryId={categoryId.Value}");

        if (removeKey != "subcategoryId" && subcategoryId.HasValue)
            queryParams.Add($"subcategoryId={subcategoryId.Value}");

        if (removeKey != "brand" && !string.IsNullOrWhiteSpace(brand))
            queryParams.Add($"brand={Uri.EscapeDataString(brand)}");

        if (removeKey != "concern" && !string.IsNullOrWhiteSpace(concern))
            queryParams.Add($"concern={Uri.EscapeDataString(concern)}");

        if (removeKey != "price")
        {
            if (minPrice.HasValue) queryParams.Add($"minPrice={minPrice.Value}");
            if (maxPrice.HasValue) queryParams.Add($"maxPrice={maxPrice.Value}");
        }

        if (removeKey != "minRating" && minRating.HasValue)
            queryParams.Add($"minRating={minRating.Value}");

        if (removeKey != "minDiscount" && minDiscount.HasValue)
            queryParams.Add($"minDiscount={minDiscount.Value}");

        if (removeKey != "inStockOnly" && inStockOnly)
            queryParams.Add("inStockOnly=true");

        if (!string.IsNullOrWhiteSpace(sort) && sort != "relevance")
            queryParams.Add($"sort={Uri.EscapeDataString(sort)}");

        return queryParams.Count > 0 ? $"/search?{string.Join("&", queryParams)}" : "/search";
    }
}
