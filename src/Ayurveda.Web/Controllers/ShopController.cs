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

    public async Task<IActionResult> Index(
        int? categoryId,
        int? subcategoryId,
        string? brand,
        decimal? minPrice,
        decimal? maxPrice,
        double? minRating,
        int? minDiscount,
        bool inStockOnly = false,
        string? query = null,
        string? sort = "popular",
        string? concern = null,
        string viewMode = "grid",
        int page = 1)
    {
        const int pageSize = 12;
        var q = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .Where(p => p.IsActive);

        // 1. Filter by Category
        if (categoryId.HasValue)
        {
            q = q.Where(p => p.CategoryId == categoryId.Value);
        }

        // 2. Filter by Subcategory
        if (subcategoryId.HasValue)
        {
            q = q.Where(p => p.SubcategoryId == subcategoryId.Value);
        }

        // 3. Filter by Brand
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var brandTrimmed = brand.Trim();
            q = q.Where(p => p.Brand == brandTrimmed);
        }

        // 4. Filter by Price Range
        if (minPrice.HasValue)
        {
            q = q.Where(p => p.Price >= minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            q = q.Where(p => p.Price <= maxPrice.Value);
        }

        // 5. Filter by Minimum Rating
        if (minRating.HasValue)
        {
            q = q.Where(p => p.AverageRating >= minRating.Value);
        }

        // 6. Filter by Minimum Discount
        if (minDiscount.HasValue)
        {
            q = q.Where(p => p.DiscountPercent >= minDiscount.Value);
        }

        // 7. Filter by Stock Availability
        if (inStockOnly)
        {
            q = q.Where(p => p.Stock > 0);
        }

        // 8. Filter by Search Query
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(p => p.Title.ToLower().Contains(term) ||
                             p.Brand.ToLower().Contains(term) ||
                             (p.Tags != null && p.Tags.ToLower().Contains(term)) ||
                             (p.TraditionallyUsedFor != null && p.TraditionallyUsedFor.ToLower().Contains(term)));
        }

        // 9. Filter by Health Concern
        if (!string.IsNullOrWhiteSpace(concern))
        {
            var concernTerm = concern.Trim().ToLower();
            q = q.Where(p => (p.TraditionallyUsedFor != null && p.TraditionallyUsedFor.ToLower().Contains(concernTerm)) ||
                             (p.Tags != null && p.Tags.ToLower().Contains(concernTerm)) ||
                             p.Category.Name.ToLower().Contains(concernTerm));
        }

        // Sorting
        q = sort switch
        {
            "price_asc" => q.OrderBy(p => p.Price),
            "price_desc" => q.OrderByDescending(p => p.Price),
            "rating" => q.OrderByDescending(p => p.AverageRating),
            "newest" => q.OrderByDescending(p => p.Id),
            "discount" => q.OrderByDescending(p => p.DiscountPercent),
            _ => q.OrderByDescending(p => p.AverageRating).ThenByDescending(p => p.Id)
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
                Badge = p.AverageRating >= 4.9 ? "Bestseller" : (p.DiscountPercent >= 20 ? $"{p.DiscountPercent}% Off" : null),
                Sku = p.Sku,
                TraditionallyUsedFor = p.TraditionallyUsedFor
            })
            .ToListAsync();

        // Load Categories with counts
        var categories = await _context.Categories
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

        // Load Subcategories (scoped to selected category if any, else top ones)
        var subQ = _context.Subcategories.Where(s => s.IsActive);
        if (categoryId.HasValue)
        {
            subQ = subQ.Where(s => s.CategoryId == categoryId.Value);
        }
        var subcategories = await subQ
            .OrderBy(s => s.Name)
            .Select(s => new SubcategoryFilterItem
            {
                Id = s.Id,
                CategoryId = s.CategoryId,
                Name = s.Name,
                ProductCount = s.Products.Count(p => p.IsActive),
                IsSelected = subcategoryId.HasValue && subcategoryId.Value == s.Id
            })
            .ToListAsync();

        // Load Brands with counts
        var brands = await _context.Products
            .Where(p => p.IsActive)
            .GroupBy(p => p.Brand)
            .Select(g => new BrandFilterItem
            {
                Name = g.Key,
                ProductCount = g.Count(),
                IsSelected = !string.IsNullOrWhiteSpace(brand) && brand == g.Key
            })
            .OrderByDescending(b => b.ProductCount)
            .ToListAsync();

        // Build Active Filter Chips
        var activeChips = new List<ActiveFilterChip>();

        if (categoryId.HasValue)
        {
            var catName = categories.FirstOrDefault(c => c.Id == categoryId.Value)?.Name ?? "Category";
            activeChips.Add(new ActiveFilterChip
            {
                Key = "categoryId",
                Label = $"Category: {catName}",
                RemoveUrl = BuildFilterUrl(null, subcategoryId, brand, minPrice, maxPrice, minRating, minDiscount, inStockOnly, query, sort, concern, viewMode)
            });
        }

        if (subcategoryId.HasValue)
        {
            var subName = subcategories.FirstOrDefault(s => s.Id == subcategoryId.Value)?.Name ?? "Subcategory";
            activeChips.Add(new ActiveFilterChip
            {
                Key = "subcategoryId",
                Label = $"Subcategory: {subName}",
                RemoveUrl = BuildFilterUrl(categoryId, null, brand, minPrice, maxPrice, minRating, minDiscount, inStockOnly, query, sort, concern, viewMode)
            });
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "brand",
                Label = $"Brand: {brand}",
                RemoveUrl = BuildFilterUrl(categoryId, subcategoryId, null, minPrice, maxPrice, minRating, minDiscount, inStockOnly, query, sort, concern, viewMode)
            });
        }

        if (minPrice.HasValue || maxPrice.HasValue)
        {
            var priceLabel = minPrice.HasValue && maxPrice.HasValue
                ? $"₹{minPrice:N0} - ₹{maxPrice:N0}"
                : minPrice.HasValue ? $"Above ₹{minPrice:N0}" : $"Under ₹{maxPrice:N0}";

            activeChips.Add(new ActiveFilterChip
            {
                Key = "price",
                Label = priceLabel,
                RemoveUrl = BuildFilterUrl(categoryId, subcategoryId, brand, null, null, minRating, minDiscount, inStockOnly, query, sort, concern, viewMode)
            });
        }

        if (minRating.HasValue)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "minRating",
                Label = $"{minRating:0.0}★ & above",
                RemoveUrl = BuildFilterUrl(categoryId, subcategoryId, brand, minPrice, maxPrice, null, minDiscount, inStockOnly, query, sort, concern, viewMode)
            });
        }

        if (minDiscount.HasValue)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "minDiscount",
                Label = $"{minDiscount}% Off or more",
                RemoveUrl = BuildFilterUrl(categoryId, subcategoryId, brand, minPrice, maxPrice, minRating, null, inStockOnly, query, sort, concern, viewMode)
            });
        }

        if (inStockOnly)
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "inStockOnly",
                Label = "In Stock Only",
                RemoveUrl = BuildFilterUrl(categoryId, subcategoryId, brand, minPrice, maxPrice, minRating, minDiscount, false, query, sort, concern, viewMode)
            });
        }

        if (!string.IsNullOrWhiteSpace(concern))
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "concern",
                Label = $"Concern: {concern}",
                RemoveUrl = BuildFilterUrl(categoryId, subcategoryId, brand, minPrice, maxPrice, minRating, minDiscount, inStockOnly, query, sort, null, viewMode)
            });
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            activeChips.Add(new ActiveFilterChip
            {
                Key = "query",
                Label = $"Search: {query}",
                RemoveUrl = BuildFilterUrl(categoryId, subcategoryId, brand, minPrice, maxPrice, minRating, minDiscount, inStockOnly, null, sort, concern, viewMode)
            });
        }

        var availableConcerns = new List<string>
        {
            "Immunity", "Digestion", "Joint Pain", "Skin Radiance", "Hair Fall", "Stress & Anxiety", "Deep Sleep", "Blood Sugar", "PCOS & Cycle", "Detox"
        };

        var model = new CatalogListViewModel
        {
            Products = products,
            Categories = categories,
            Subcategories = subcategories,
            Brands = brands,
            ActiveChips = activeChips,
            AvailableConcerns = availableConcerns,
            TotalCount = totalCount,
            PageIndex = page,
            PageSize = pageSize,
            SelectedCategoryId = categoryId,
            SelectedSubcategoryId = subcategoryId,
            SelectedBrand = brand,
            SelectedSort = sort ?? "popular",
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            MinRating = minRating,
            MinDiscount = minDiscount,
            InStockOnly = inStockOnly,
            SearchQuery = query,
            Concern = concern,
            ViewMode = viewMode
        };

        ViewBag.ShopBanners = await _context.Banners
            .Where(b => b.IsActive && b.Placement == "shop")
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();

        return View(model);
    }

    private string BuildFilterUrl(
        int? categoryId,
        int? subcategoryId,
        string? brand,
        decimal? minPrice,
        decimal? maxPrice,
        double? minRating,
        int? minDiscount,
        bool inStockOnly,
        string? query,
        string? sort,
        string? concern,
        string viewMode)
    {
        var queryParams = new List<string>();
        if (categoryId.HasValue) queryParams.Add($"categoryId={categoryId.Value}");
        if (subcategoryId.HasValue) queryParams.Add($"subcategoryId={subcategoryId.Value}");
        if (!string.IsNullOrWhiteSpace(brand)) queryParams.Add($"brand={Uri.EscapeDataString(brand)}");
        if (minPrice.HasValue) queryParams.Add($"minPrice={minPrice.Value}");
        if (maxPrice.HasValue) queryParams.Add($"maxPrice={maxPrice.Value}");
        if (minRating.HasValue) queryParams.Add($"minRating={minRating.Value}");
        if (minDiscount.HasValue) queryParams.Add($"minDiscount={minDiscount.Value}");
        if (inStockOnly) queryParams.Add("inStockOnly=true");
        if (!string.IsNullOrWhiteSpace(query)) queryParams.Add($"query={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(sort) && sort != "popular") queryParams.Add($"sort={sort}");
        if (!string.IsNullOrWhiteSpace(concern)) queryParams.Add($"concern={Uri.EscapeDataString(concern)}");
        if (!string.IsNullOrWhiteSpace(viewMode) && viewMode != "grid") queryParams.Add($"viewMode={viewMode}");

        return queryParams.Any() ? $"/Shop?{string.Join("&", queryParams)}" : "/Shop";
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .Include(p => p.Questions)
            .Include(p => p.Reviews)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product == null)
            return NotFound();

        // Track recently viewed in cookie
        TrackRecentlyViewedProduct(id);

        // Related Products from same category
        var relatedProducts = await _context.Products
            .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.IsActive)
            .Take(4)
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
                Stock = p.Stock
            })
            .ToListAsync();

        // Frequently Bought Together (companion item)
        var companion = await _context.Products
            .Where(p => p.Id != product.Id && p.IsActive && p.Stock > 0)
            .OrderBy(p => p.Price)
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
                Stock = p.Stock
            })
            .FirstOrDefaultAsync();

        // Recently Viewed items
        var recentlyViewedIds = GetRecentlyViewedIds().Where(pid => pid != id).Take(4).ToList();
        var recentlyViewedProducts = await _context.Products
            .Where(p => recentlyViewedIds.Contains(p.Id) && p.IsActive)
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
                Stock = p.Stock
            })
            .ToListAsync();

        var model = new ProductDetailsViewModel
        {
            Id = product.Id,
            Title = product.Title,
            Description = product.Description,
            Brand = product.Brand,
            CategoryName = product.Category.Name,
            CategoryId = product.CategoryId,
            SubcategoryName = product.Subcategory?.Name,
            SubcategoryId = product.SubcategoryId,
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
            AdditionalImages = product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
            Variants = product.Variants.Select(v => new ProductVariantViewModel
            {
                Id = v.Id,
                Title = v.Title,
                SizeOrPack = v.SizeOrPack,
                Price = v.Price,
                Mrp = v.Mrp,
                StockQuantity = v.StockQuantity,
                IsActive = v.IsActive
            }).ToList(),
            Reviews = product.Reviews.OrderByDescending(r => r.CreatedAt).Select(r => new ProductReviewViewModel
            {
                Id = r.Id,
                UserName = r.User != null ? r.User.FullName : "Ayurveda Practitioner",
                Rating = r.Rating,
                ReviewText = r.ReviewText,
                IsVerifiedPurchase = r.IsVerifiedPurchase,
                HelpfulVotes = r.HelpfulVotes,
                CreatedAt = r.CreatedAt
            }).ToList(),
            Questions = product.Questions.OrderByDescending(q => q.CreatedAt).Select(q => new ProductFaqViewModel
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                AnswerText = q.AnswerText,
                AskedBy = q.AskedBy,
                AnsweredBy = q.AnsweredBy,
                CreatedAt = q.CreatedAt
            }).ToList(),
            RelatedProducts = relatedProducts,
            FrequentlyBoughtTogether = companion,
            RecentlyViewed = recentlyViewedProducts
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult CheckPincode(string? pincode)
    {
        if (string.IsNullOrWhiteSpace(pincode) || pincode.Trim().Length != 6 || !pincode.All(char.IsDigit))
        {
            return Json(new PincodeCheckResultViewModel
            {
                Pincode = pincode ?? "",
                IsServiceable = false,
                Message = "Please enter a valid 6-digit Indian PIN code."
            });
        }

        var pin = pincode.Trim();
        var prefix = pin[0];
        string regionName;
        int deliveryDays;

        switch (prefix)
        {
            case '1':
            case '2':
                regionName = "North India (Delhi, Haryana, Punjab, UP)";
                deliveryDays = 2;
                break;
            case '3':
            case '4':
                regionName = "West India (Maharashtra, Gujarat, Rajasthan)";
                deliveryDays = 3;
                break;
            case '5':
            case '6':
                regionName = "South India (Karnataka, Tamil Nadu, Kerala, AP)";
                deliveryDays = 3;
                break;
            case '7':
            case '8':
                regionName = "East & Central India (Bengal, Bihar, Odisha, MP)";
                deliveryDays = 4;
                break;
            default:
                regionName = "All India Standard Coverage";
                deliveryDays = 5;
                break;
        }

        var etaDate = DateTime.Now.AddDays(deliveryDays).ToString("ddd, dd MMM");

        return Json(new PincodeCheckResultViewModel
        {
            Pincode = pin,
            IsServiceable = true,
            CityState = regionName,
            EstimatedDeliveryDate = etaDate,
            IsCodAvailable = true,
            Message = $"Serviceable to {regionName}. Estimated delivery by {etaDate}."
        });
    }

    [HttpGet]
    public async Task<IActionResult> Compare(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return View(new CompareViewModel());
        }

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var parsed) ? parsed : 0)
            .Where(id => id > 0)
            .Distinct()
            .Take(3)
            .ToList();

        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => idList.Contains(p.Id) && p.IsActive)
            .Select(p => new CompareProductViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Brand = p.Brand,
                CategoryName = p.Category.Name,
                Price = p.Price,
                Mrp = p.Mrp,
                ImageUrl = p.ImageUrl,
                AverageRating = p.AverageRating,
                Stock = p.Stock,
                TraditionallyUsedFor = p.TraditionallyUsedFor,
                Ingredients = p.Ingredients,
                HowToUse = p.HowToUse,
                Precautions = p.Precautions
            })
            .ToListAsync();

        return View(new CompareViewModel { Products = products });
    }

    private void TrackRecentlyViewedProduct(int productId)
    {
        var existing = GetRecentlyViewedIds();
        existing.Remove(productId);
        existing.Insert(0, productId);
        var truncated = existing.Take(8);
        Response.Cookies.Append("TGA_RecentlyViewed", string.Join(",", truncated), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });
    }

    private List<int> GetRecentlyViewedIds()
    {
        if (Request.Cookies.TryGetValue("TGA_RecentlyViewed", out var cookieValue) && !string.IsNullOrWhiteSpace(cookieValue))
        {
            return cookieValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();
        }
        return new List<int>();
    }
}
