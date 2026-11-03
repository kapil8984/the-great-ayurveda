using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Infrastructure.Services;

public class SearchService : ISearchService
{
    private readonly AyurvedaDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SearchService> _logger;

    // Classical Ayurvedic Herbs Dictionary for Herb Autocomplete
    private static readonly (string Name, string Botanical, string PrimaryBenefit)[] ClassicalHerbs = new[]
    {
        ("Ashwagandha", "Withania somnifera", "Stress resilience, physical stamina & sound sleep"),
        ("Triphala", "Emblica officinalis, Terminalia chebula, Terminalia bellirica", "Digestive regularizer & natural colon cleanser"),
        ("Chyawanprash", "Classical 40+ herb Rasayana", "Vitality, seasonal immunity & Ojas restorative"),
        ("Brahmi", "Bacopa monnieri", "Medhya Rasayana for memory, focus & cognitive calm"),
        ("Giloy (Amrita)", "Tinospora cordifolia", "Pitta pacifier, leukocyte tone & liver purifier"),
        ("Shatavari", "Asparagus racemosus", "Female hormonal harmony & natural adaptogen"),
        ("Kumkumadi Tailam", "Crocus sativus & 26 botanicals", "Radiance, dark-spot fading & youthful glow"),
        ("Mahanarayan Taila", "Dashamula, Bala & Sesame oil", "Relief for stiff joints & comforting muscle aches"),
        ("Amla (Amalaki)", "Emblica officinalis", "Natural heat-stable Vitamin C & antioxidant"),
        ("Neelibhringadi", "Indigofera tinctoria & Eclipta alba", "Scalp cooling, dandruff protection & dark lustrous hair"),
        ("Anu Thailam", "Classical 28-herb Nasya oil", "Sinus clearing, respiratory prana & mental sharpness"),
        ("Gokshura", "Tribulus terrestris", "Kidney vitality, urinary tract comfort & physical vigor"),
        ("Shallaki (Boswellia)", "Boswellia serrata", "Joint cartilage lubrication & mobility ease"),
        ("A2 Gir Cow Ghee", "Vedic Bilona clarified butter", "Ojas builder, digestive fire kindle & cognitive nourishment"),
        ("Hadjod", "Cissus quadrangularis", "Bone density & tendon recovery support")
    };

    // Hindi & Hinglish Transliteration / Synonym Dictionary
    private static readonly Dictionary<string, (string CanonicalTerm, string RelatedCategory)> HinglishSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bukhar"] = ("Fever", "Immunity & Rasayana"),
        ["tap"] = ("Fever", "Immunity & Rasayana"),
        ["baal jhadna"] = ("Hair Fall", "Hair Care"),
        ["baal"] = ("Hair Care", "Hair Care"),
        ["hairfall"] = ("Hair Fall", "Hair Care"),
        ["kesh"] = ("Hair Care", "Hair Care"),
        ["pet dard"] = ("Digestive Discomfort", "Digestive Care"),
        ["kabz"] = ("Constipation", "Digestive Care"),
        ["gas"] = ("Bloating & Gas", "Digestive Care"),
        ["apach"] = ("Indigestion", "Digestive Care"),
        ["neend"] = ("Sleep Support", "Stress, Sleep & Brain"),
        ["anidra"] = ("Insomnia", "Stress, Sleep & Brain"),
        ["tanaav"] = ("Stress", "Stress, Sleep & Brain"),
        ["chinta"] = ("Anxiety", "Stress, Sleep & Brain"),
        ["jod dard"] = ("Joint Pain", "Joint & Muscle Care"),
        ["ghutna"] = ("Knee Pain", "Joint & Muscle Care"),
        ["gathiya"] = ("Arthritis / Sandhivata", "Joint & Muscle Care"),
        ["khansi"] = ("Cough", "Immunity & Rasayana"),
        ["sardi"] = ("Cold", "Immunity & Rasayana"),
        ["jukaam"] = ("Cold & Catarrh", "Immunity & Rasayana"),
        ["sugar"] = ("Diabetes Support", "Diabetes & Metabolism Support"),
        ["madhumeh"] = ("Diabetes Support", "Diabetes & Metabolism Support"),
        ["chehra"] = ("Face Radiance", "Skin Care"),
        ["muhase"] = ("Acne & Pimples", "Skin Care"),
        ["keel"] = ("Acne & Blemishes", "Skin Care"),
        ["kamzori"] = ("General Debility", "Immunity & Rasayana"),
        ["thakan"] = ("Fatigue", "Immunity & Rasayana"),
        ["vajan"] = ("Weight Support", "Diabetes & Metabolism Support"),
        ["motapa"] = ("Metabolic Balance", "Diabetes & Metabolism Support")
    };

    // Keywords for Levenshtein Distance Typo Correction
    private static readonly string[] TypoKeywords = new[]
    {
        "Chyawanprash", "Ashwagandha", "Triphala", "Kumkumadi", "Mahanarayan",
        "Brahmi", "Giloy", "Shatavari", "Gokshura", "Neelibhringadi",
        "Anu Thailam", "Amalaki", "Karela Jamun", "A2 Ghee", "Hadjod",
        "Shallaki", "Drakshasava", "Avipattikar", "Shikakai", "Jeerakarishta",
        "Ashokarishta", "Kanchanar", "Kasturi Manjal", "Satadhauta Ghritha"
    };

    private static readonly List<string> DefaultTrending = new()
    {
        "Chyawanprash", "Ashwagandha", "Triphala", "Kumkumadi Tailam", "A2 Gir Cow Ghee", "Giloy Ghanvati", "Hair Fall Oil"
    };

    public SearchService(
        AyurvedaDbContext context,
        IMemoryCache cache,
        ILogger<SearchService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SearchSuggestionResultDto> GetSuggestionsAsync(string query, string scope = "public")
    {
        var stopwatch = Stopwatch.StartNew();
        var normalizedQuery = query?.Trim() ?? string.Empty;

        var result = new SearchSuggestionResultDto
        {
            Query = normalizedQuery,
            TrendingSearches = DefaultTrending
        };

        if (string.IsNullOrWhiteSpace(normalizedQuery) || normalizedQuery.Length < 2)
        {
            stopwatch.Stop();
            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            return result;
        }

        // Cache Key
        var cacheKey = $"tga_search_suggest_{scope}_{normalizedQuery.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out SearchSuggestionResultDto? cached) && cached != null)
        {
            stopwatch.Stop();
            cached.LatencyMs = stopwatch.ElapsedMilliseconds;
            return cached;
        }

        var term = normalizedQuery.ToLowerInvariant();
        string effectiveSearchTerm = term;

        // 1. Check Hindi/Hinglish Transliteration Synonym
        if (HinglishSynonyms.TryGetValue(term, out var synonym))
        {
            result.SynonymApplied = $"{synonym.CanonicalTerm} ({synonym.RelatedCategory})";
            effectiveSearchTerm = synonym.CanonicalTerm.ToLowerInvariant();
        }

        // 2. Check Typo Tolerance (Levenshtein Distance)
        var nearest = FindNearestKeyword(term);
        if (!string.IsNullOrEmpty(nearest) && !nearest.Equals(term, StringComparison.OrdinalIgnoreCase))
        {
            result.DidYouMean = nearest;
        }

        // 3. Handle Admin Scope
        if (scope.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            result.AdminOrders = await _context.Orders
                .Where(o => (o.OrderNumber != null && o.OrderNumber.ToLower().Contains(term)) ||
                            (o.User != null && o.User.Email != null && o.User.Email.ToLower().Contains(term)) ||
                            (o.ShippingAddress != null && o.ShippingAddress.ToLower().Contains(term)))
                .Take(5)
                .Select(o => new SearchItemDto
                {
                    Id = o.Id.ToString(),
                    Title = $"Order #{o.OrderNumber}",
                    Subtitle = $"Total: ₹{o.TotalAmount:N0} • {o.Status}",
                    Url = $"/Admin/Orders/Details/{o.Id}",
                    Badge = o.Status,
                    Price = o.TotalAmount
                })
                .ToListAsync();

            result.AdminUsers = await _context.Users
                .Where(u => (u.Email != null && u.Email.ToLower().Contains(term)) ||
                            (u.FullName != null && u.FullName.ToLower().Contains(term)) ||
                            (u.ContactNumber != null && u.ContactNumber.Contains(term)))
                .Take(5)
                .Select(u => new SearchItemDto
                {
                    Id = u.Id.ToString(),
                    Title = u.FullName,
                    Subtitle = $"{u.Email} • {u.ContactNumber}",
                    Url = $"/Admin/Users/Details/{u.Id}",
                    Badge = "User"
                })
                .ToListAsync();

            result.Doctors = await _context.Doctors
                .Where(d => d.DoctorName.ToLower().Contains(term) || d.Email.ToLower().Contains(term))
                .Take(4)
                .Select(d => new SearchItemDto
                {
                    Id = d.Id.ToString(),
                    Title = d.DoctorName.StartsWith("Dr.") ? d.DoctorName : "Dr. " + d.DoctorName,
                    Subtitle = d.Email,
                    ImageUrl = d.ProfileImage,
                    Url = $"/Admin/Doctors/Details/{d.Id}",
                    Badge = d.DoctorDetail != null && d.DoctorDetail.Status == 1 ? "Approved" : "Pending"
                })
                .ToListAsync();

            result.AdminProducts = await _context.Products
                .Where(p => p.Title.ToLower().Contains(term) || p.Brand.ToLower().Contains(term) || (p.Sku != null && p.Sku.ToLower().Contains(term)))
                .Take(5)
                .Select(p => new SearchItemDto
                {
                    Id = p.Id.ToString(),
                    Title = p.Title,
                    Subtitle = $"SKU: {p.Sku} • Stock: {p.Stock} • ₹{p.Price:N0}",
                    ImageUrl = p.ImageUrl,
                    Url = $"/Admin/Products/Edit/{p.Id}",
                    Badge = p.Stock > 0 ? "In Stock" : "Out of Stock",
                    Price = p.Price
                })
                .ToListAsync();

            stopwatch.Stop();
            result.LatencyMs = stopwatch.ElapsedMilliseconds;

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(5)
            });

            return result;
        }

        // 4. Handle Public Storefront Scope: Products, Categories, Herbs, Concerns, Doctors

        // A. Products Search (by title, brand, tags, traditional use, or synonym)
        result.Products = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive &&
                       (p.Title.ToLower().Contains(term) ||
                        p.Title.ToLower().Contains(effectiveSearchTerm) ||
                        p.Brand.ToLower().Contains(term) ||
                        (p.Tags != null && (p.Tags.ToLower().Contains(term) || p.Tags.ToLower().Contains(effectiveSearchTerm))) ||
                        (p.TraditionallyUsedFor != null && (p.TraditionallyUsedFor.ToLower().Contains(term) || p.TraditionallyUsedFor.ToLower().Contains(effectiveSearchTerm)))))
            .Take(6)
            .Select(p => new SearchItemDto
            {
                Id = p.Id.ToString(),
                Title = p.Title,
                Subtitle = $"by {p.Brand} • {p.Category.Name}",
                ImageUrl = p.ImageUrl,
                Url = $"/Shop/Details/{p.Id}",
                Badge = p.Stock > 0 ? (p.AverageRating >= 4.8 ? "Bestseller" : "In Stock") : "Out of Stock",
                Price = p.Price,
                Rating = p.AverageRating
            })
            .ToListAsync();

        // B. Categories Search
        result.Categories = await _context.Categories
            .Where(c => c.IsActive && (c.Name.ToLower().Contains(term) || c.Name.ToLower().Contains(effectiveSearchTerm)))
            .Take(4)
            .Select(c => new SearchItemDto
            {
                Id = c.Id.ToString(),
                Title = c.Name,
                Subtitle = $"{c.Products.Count(p => p.IsActive)} Formulations",
                ImageUrl = c.ImageUrl,
                Url = $"/Shop?categoryId={c.Id}",
                Badge = "Category"
            })
            .ToListAsync();

        // C. Classical Ayurvedic Herbs Match
        result.Herbs = ClassicalHerbs
            .Where(h => h.Name.ToLower().Contains(term) || h.Botanical.ToLower().Contains(term) || h.PrimaryBenefit.ToLower().Contains(term))
            .Take(3)
            .Select(h => new SearchItemDto
            {
                Title = h.Name,
                Subtitle = $"{h.Botanical} — {h.PrimaryBenefit}",
                Url = $"/Shop?query={Uri.EscapeDataString(h.Name)}",
                Badge = "Classical Herb"
            })
            .ToList();

        // D. Concerns / Doshas Match
        var concernsList = new (string Name, string Description)[]
        {
            ("Immunity & Rasayana", "Traditional formulations to support vitality, Ojas, and defenses"),
            ("Digestive Health & Agni", "Churnas and herbs to relieve gas, acidity, and sluggish bowels"),
            ("Joint & Muscle Comfort", "Classical therapeutic oils for stiffness, arthritis, and backache"),
            ("Stress, Anxiety & Sleep", "Adaptogens like Ashwagandha and Tagara for restful sleep"),
            ("Hair Fall & Scalp Health", "Bhringraj, Amla, and herbs for strong roots and dark shine"),
            ("Skin Radiance & Blemish Care", "Kumkumadi, Sandalwood, and herbal ubtans for natural glow"),
            ("Diabetes & Sugar Metabolism", "Karela, Jamun, and Vijaysar to support healthy blood sugar")
        };

        result.Concerns = concernsList
            .Where(c => c.Name.ToLower().Contains(term) || c.Name.ToLower().Contains(effectiveSearchTerm) || c.Description.ToLower().Contains(term) || c.Description.ToLower().Contains(effectiveSearchTerm))
            .Take(3)
            .Select(c => new SearchItemDto
            {
                Title = c.Name,
                Subtitle = c.Description,
                Url = $"/Shop?concern={Uri.EscapeDataString(c.Name.Split('&')[0].Trim())}",
                Badge = "Health Concern"
            })
            .ToList();

        // E. Verified Doctors Match
        result.Doctors = await _context.Doctors
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1 &&
                       (d.DoctorName.ToLower().Contains(term) || (d.DoctorDetail.Specialization != null && d.DoctorDetail.Specialization.Name.ToLower().Contains(term))))
            .Take(3)
            .Select(d => new SearchItemDto
            {
                Id = d.Id.ToString(),
                Title = d.DoctorName.StartsWith("Dr.") ? d.DoctorName : "Dr. " + d.DoctorName,
                Subtitle = d.DoctorDetail != null && d.DoctorDetail.Specialization != null ? $"{d.DoctorDetail.Specialization.Name} • {d.DoctorDetail.Experience} yrs exp" : "Ayurvedic Doctor",
                ImageUrl = d.ProfileImage,
                Url = $"/Doctors/Profile/{d.Id}",
                Badge = "Available for Consult"
            })
            .ToListAsync();

        stopwatch.Stop();
        result.LatencyMs = stopwatch.ElapsedMilliseconds;

        // Cache hot queries (5 min sliding TTL)
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5)
        });

        return result;
    }

    private static string? FindNearestKeyword(string query)
    {
        if (query.Length < 4) return null;

        string? bestMatch = null;
        int minDistance = int.MaxValue;

        foreach (var keyword in TypoKeywords)
        {
            var dist = ComputeLevenshteinDistance(query.ToLowerInvariant(), keyword.ToLowerInvariant());
            // Allow distance of 1 or 2 for reasonable typos
            if (dist <= 2 && dist < minDistance)
            {
                minDistance = dist;
                bestMatch = keyword;
            }
        }

        return minDistance <= 2 ? bestMatch : null;
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
