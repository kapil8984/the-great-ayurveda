using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Infrastructure.Services;

public class CatalogImportService : ICatalogImportService
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<CatalogImportService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory? _httpClientFactory;
    private static readonly ConcurrentDictionary<string, bool> _verifiedUrlCache = new(StringComparer.OrdinalIgnoreCase);

    public CatalogImportService(
        AyurvedaDbContext context,
        IAuditService auditService,
        ILogger<CatalogImportService> logger)
        : this(context, auditService, new ConfigurationBuilder().Build(), logger, null)
    {
    }

    public CatalogImportService(
        AyurvedaDbContext context,
        IAuditService auditService,
        IConfiguration configuration,
        ILogger<CatalogImportService> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _context = context;
        _auditService = auditService;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<CatalogSystemStatusViewModel> GetSystemStatusAsync(CancellationToken ct = default)
    {
        var (catalogData, jsonPath) = await LoadCatalogJsonAsync(ct);
        var dbProducts = await _context.Products.AsNoTracking().ToListAsync(ct);
        var totalProductImages = await _context.ProductImages.AsNoTracking().CountAsync(ct);

        var genericCount = dbProducts.Count(p => IsGenericOrPlaceholderImage(p.ImageUrl));
        var validCount = dbProducts.Count - genericCount;

        var catalogTitles = catalogData.Select(c => NormalizeTitle(c.Title)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dbTitles = dbProducts.Select(p => NormalizeTitle(p.Title)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingCount = catalogTitles.Count(t => !dbTitles.Contains(t));

        return new CatalogSystemStatusViewModel
        {
            DatabaseProductCount = dbProducts.Count,
            CatalogJsonProductCount = catalogData.Count,
            MissingProductsCount = missingCount,
            GenericOrPlaceholderImageCount = genericCount,
            ValidProductImageCount = validCount,
            TotalProductImagesRows = totalProductImages,
            CatalogJsonPath = jsonPath ?? "Not Found",
            CatalogJsonExists = !string.IsNullOrEmpty(jsonPath),
            CheckedAt = DateTime.UtcNow
        };
    }

    public async Task<CatalogImportPreviewResult> PreviewProductImportAsync(CancellationToken ct = default)
    {
        if (!_configuration.GetValue<bool>("CatalogImport:Enabled", true))
        {
            return new CatalogImportPreviewResult
            {
                TotalCatalogItems = 0,
                Message = "Catalogue import is disabled by configuration (CatalogImport:Enabled=false)."
            };
        }

        var (catalogData, jsonPath) = await LoadCatalogJsonAsync(ct);
        if (!catalogData.Any())
        {
            return new CatalogImportPreviewResult
            {
                TotalCatalogItems = 0,
                Message = $"Failed to locate or parse catalog_300plus.json. Searched base: {AppContext.BaseDirectory}"
            };
        }

        var dbProducts = await _context.Products.AsNoTracking().ToListAsync(ct);

        var result = new CatalogImportPreviewResult
        {
            TotalCatalogItems = catalogData.Count
        };

        var skuMap = dbProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Sku))
            .GroupBy(p => p.Sku!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var titleMap = dbProducts
            .GroupBy(p => p.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var normalizedTitleMap = dbProducts
            .GroupBy(p => NormalizeTitle(p.Title), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in catalogData)
        {
            var matchCandidates = new HashSet<int>();

            if (!string.IsNullOrWhiteSpace(item.Sku) && skuMap.TryGetValue(item.Sku.Trim(), out var skuMatches))
            {
                foreach (var p in skuMatches) matchCandidates.Add(p.Id);
            }

            if (titleMap.TryGetValue(item.Title.Trim(), out var titleMatches))
            {
                foreach (var p in titleMatches) matchCandidates.Add(p.Id);
            }

            var norm = NormalizeTitle(item.Title);
            if (normalizedTitleMap.TryGetValue(norm, out var normMatches))
            {
                foreach (var p in normMatches) matchCandidates.Add(p.Id);
            }

            if (matchCandidates.Count > 1)
            {
                result.AmbiguousItems.Add(new CatalogImportAmbiguousItem
                {
                    Title = item.Title,
                    Sku = item.Sku,
                    Reason = $"Multiple database products ({string.Join(", ", matchCandidates)}) match this catalogue entry.",
                    ConflictingProductIds = matchCandidates.ToList()
                });
                result.AmbiguousMatchesCount++;
                continue;
            }

            if (matchCandidates.Count == 1)
            {
                var matchedId = matchCandidates.First();
                var existing = dbProducts.First(p => p.Id == matchedId);
                result.ExistingMatched.Add(new CatalogImportProductItem
                {
                    Title = item.Title,
                    Brand = item.Brand,
                    CategoryName = item.CategoryName,
                    SubcategoryName = item.SubcategoryName,
                    Price = item.Price,
                    Mrp = item.Mrp,
                    Stock = item.Stock,
                    Sku = item.Sku,
                    ImageUrl = item.ImageUrl,
                    AdditionalImagesCount = item.AdditionalImages?.Count ?? 0,
                    ExistingProductId = matchedId,
                    MatchReason = existing.Sku == item.Sku ? "Exact SKU Match" : "Title / Name Match"
                });
                result.ExistingMatchingCount++;
            }
            else
            {
                result.NewProducts.Add(new CatalogImportProductItem
                {
                    Title = item.Title,
                    Brand = item.Brand,
                    CategoryName = item.CategoryName,
                    SubcategoryName = item.SubcategoryName,
                    Price = item.Price,
                    Mrp = item.Mrp,
                    Stock = item.Stock,
                    Sku = item.Sku,
                    ImageUrl = item.ImageUrl,
                    AdditionalImagesCount = item.AdditionalImages?.Count ?? 0,
                    MatchReason = "New Product (No DB Match)"
                });
                result.NewProductsToInsertCount++;
            }
        }

        result.Message = $"Catalogue preview ready: {result.NewProductsToInsertCount} new products to insert, {result.ExistingMatchingCount} products already present, {result.AmbiguousMatchesCount} ambiguous items.";
        return result;
    }

    public async Task<CatalogImportExecutionResult> ExecuteProductImportAsync(string executedByUserEmail, CancellationToken ct = default)
    {
        var result = new CatalogImportExecutionResult
        {
            ExecutedBy = executedByUserEmail
        };

        if (!_configuration.GetValue<bool>("CatalogImport:Enabled", true))
        {
            result.Success = false;
            result.Errors.Add("Catalogue import is disabled by configuration (CatalogImport:Enabled=false).");
            return result;
        }

        var preview = await PreviewProductImportAsync(ct);
        var (catalogData, _) = await LoadCatalogJsonAsync(ct);

        if (!preview.NewProducts.Any())
        {
            result.Success = true;
            result.SkippedCount = preview.ExistingMatchingCount;
            result.AmbiguousCount = preview.AmbiguousMatchesCount;
            result.Errors.Add("No new products required insertion. Database catalogue is already complete.");
            return result;
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var existingCategories = await _context.Categories.ToListAsync(ct);
                var existingSubcategories = await _context.Subcategories.Include(s => s.Category).ToListAsync(ct);

                var dbProducts = await _context.Products.AsNoTracking().ToListAsync(ct);
                var existingTitles = dbProducts.Select(p => NormalizeTitle(p.Title)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingSkus = dbProducts.Where(p => !string.IsNullOrWhiteSpace(p.Sku)).Select(p => p.Sku!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newProductTitlesToInsert = preview.NewProducts.Select(p => p.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var itemsToInsert = catalogData.Where(c => newProductTitlesToInsert.Contains(c.Title)).ToList();

                int inserted = 0;
                int skipped = 0;

                foreach (var item in itemsToInsert)
                {
                    var norm = NormalizeTitle(item.Title);
                    if (existingTitles.Contains(norm))
                    {
                        skipped++;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(item.Sku) && existingSkus.Contains(item.Sku.Trim()))
                    {
                        skipped++;
                        continue;
                    }

                    // Resolve category
                    var cat = existingCategories.FirstOrDefault(c => c.Name.Equals(item.CategoryName, StringComparison.OrdinalIgnoreCase))
                              ?? existingCategories.FirstOrDefault(c => item.CategoryName.StartsWith(c.Name, StringComparison.OrdinalIgnoreCase) || c.Name.StartsWith(item.CategoryName, StringComparison.OrdinalIgnoreCase))
                              ?? existingCategories.First();

                    // Resolve subcategory
                    var sub = existingSubcategories.FirstOrDefault(s => s.CategoryId == cat.Id && s.Name.Equals(item.SubcategoryName, StringComparison.OrdinalIgnoreCase))
                              ?? existingSubcategories.FirstOrDefault(s => s.CategoryId == cat.Id)
                              ?? existingSubcategories.First();

                    int discountPercent = item.Mrp > 0 ? (int)Math.Round((1.0m - (item.Price / item.Mrp)) * 100) : 0;

                    var product = new Product
                    {
                        Title = item.Title,
                        Brand = item.Brand,
                        Description = item.Description,
                        Price = item.Price,
                        Mrp = item.Mrp,
                        DiscountPercent = discountPercent,
                        Stock = item.Stock,
                        CategoryId = cat.Id,
                        SubcategoryId = sub.Id,
                        ImageUrl = item.ImageUrl,
                        AverageRating = item.Rating > 0 ? item.Rating : 4.8,
                        Sku = item.Sku,
                        Tags = item.Tags,
                        TraditionallyUsedFor = item.TraditionallyUsedFor,
                        Ingredients = item.Ingredients,
                        HowToUse = item.HowToUse,
                        Precautions = item.Precautions,
                        IsPrescriptionRequired = item.IsPrescriptionRequired,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Add primary image to gallery
                    if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                    {
                        product.Images.Add(new ProductImage
                        {
                            ImageUrl = product.ImageUrl,
                            IsPrimary = true,
                            DisplayOrder = 1
                        });
                    }

                    // Add additional gallery angles
                    if (item.AdditionalImages != null && item.AdditionalImages.Any())
                    {
                        int order = 2;
                        foreach (var addImg in item.AdditionalImages)
                        {
                            if (!string.IsNullOrWhiteSpace(addImg) && !string.Equals(addImg, product.ImageUrl, StringComparison.OrdinalIgnoreCase))
                            {
                                product.Images.Add(new ProductImage
                                {
                                    ImageUrl = addImg,
                                    IsPrimary = false,
                                    DisplayOrder = order++
                                });
                            }
                        }
                    }

                    // Add variants
                    if (item.Variants != null && item.Variants.Any())
                    {
                        foreach (var v in item.Variants)
                        {
                            product.Variants.Add(new ProductVariant
                            {
                                Title = v.Title,
                                SizeOrPack = v.SizeOrPack,
                                Price = v.Price,
                                Mrp = v.Mrp,
                                StockQuantity = v.Stock,
                                Sku = $"{product.Sku}-{v.SizeOrPack.Replace(" ", "")}"
                            });
                        }
                    }

                    // Add reviews
                    if (item.Reviews != null && item.Reviews.Any())
                    {
                        foreach (var r in item.Reviews)
                        {
                            product.Reviews.Add(new ProductReview
                            {
                                UserId = 1,
                                Rating = r.Rating,
                                ReviewText = r.Text,
                                HelpfulVotes = r.Helpful,
                                IsVerifiedPurchase = true,
                                CreatedAt = DateTime.UtcNow.AddDays(-r.DaysAgo)
                            });
                        }
                    }

                    // Add FAQs
                    if (item.Faqs != null && item.Faqs.Any())
                    {
                        foreach (var f in item.Faqs)
                        {
                            product.Questions.Add(new ProductQuestion
                            {
                                QuestionText = f.Question,
                                AnswerText = f.Answer,
                                AskedBy = f.AskedBy,
                                AnsweredBy = "Senior Ayurvedic Vaidya, Great Ayurveda Labs",
                                CreatedAt = DateTime.UtcNow.AddDays(-10)
                            });
                        }
                    }

                    _context.Products.Add(product);
                    existingTitles.Add(norm);
                    if (!string.IsNullOrWhiteSpace(product.Sku)) existingSkus.Add(product.Sku.Trim());

                    result.InsertedProductTitles.Add(product.Title);
                    inserted++;
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                result.Success = true;
                result.InsertedCount = inserted;
                result.SkippedCount = skipped + preview.ExistingMatchingCount;
                result.AmbiguousCount = preview.AmbiguousMatchesCount;

                await _auditService.LogAsync(
                    "CatalogImport",
                    "Product",
                    null,
                    $"Successfully imported {inserted} new products into database catalogue. Existing products preserved untouched.",
                    executedByUserEmail);

                _logger.LogInformation("CatalogImport executed by {User}: Inserted {Inserted}, Skipped {Skipped}.", executedByUserEmail, inserted, result.SkippedCount);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Transaction rolled back during CatalogImport execution.");
                result.Success = false;
                result.Errors.Add($"Import transaction failed and was rolled back: {ex.Message}");
            }
        });

        return result;
    }

    public async Task<ImageMappingPreviewResult> PreviewImageMappingAsync(CancellationToken ct = default)
    {
        if (!_configuration.GetValue<bool>("CatalogImport:Enabled", true))
        {
            return new ImageMappingPreviewResult
            {
                TotalCatalogItems = 0,
                TotalDatabaseProducts = 0,
                Message = "Image mapping is disabled by configuration (CatalogImport:Enabled=false)."
            };
        }

        var (catalogData, _) = await LoadCatalogJsonAsync(ct);
        var dbProducts = await _context.Products.Include(p => p.Images).AsNoTracking().ToListAsync(ct);

        var result = new ImageMappingPreviewResult
        {
            TotalCatalogItems = catalogData.Count,
            TotalDatabaseProducts = dbProducts.Count
        };

        var catalogBySku = catalogData
            .Where(c => !string.IsNullOrWhiteSpace(c.Sku))
            .GroupBy(c => c.Sku!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var catalogByTitle = catalogData
            .GroupBy(c => c.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var catalogByNorm = catalogData
            .GroupBy(c => NormalizeTitle(c.Title), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var p in dbProducts)
        {
            ProductJsonCatalogItem? matchedCatalog = null;

            if (!string.IsNullOrWhiteSpace(p.Sku) && catalogBySku.TryGetValue(p.Sku.Trim(), out var bySku))
            {
                matchedCatalog = bySku;
            }
            else if (catalogByTitle.TryGetValue(p.Title.Trim(), out var byTitle))
            {
                matchedCatalog = byTitle;
            }
            else if (catalogByNorm.TryGetValue(NormalizeTitle(p.Title), out var byNorm))
            {
                matchedCatalog = byNorm;
            }

            if (matchedCatalog == null)
            {
                result.Items.Add(new ImageMappingPreviewItem
                {
                    ProductId = p.Id,
                    Title = p.Title,
                    Brand = p.Brand,
                    Sku = p.Sku,
                    CurrentPrimaryImage = p.ImageUrl,
                    ProposedPrimaryImage = p.ImageUrl,
                    CurrentGalleryCount = p.Images.Count,
                    Status = ImageMappingStatus.ProductNotFoundInCatalog,
                    StatusDetails = "Custom product not found in standard catalog_300plus.json. Preserved unchanged."
                });
                continue;
            }

            result.ExistingMatchingCount++;

            // Check if admin manually uploaded image
            if (IsAdminUploadedImage(p.ImageUrl))
            {
                result.Items.Add(new ImageMappingPreviewItem
                {
                    ProductId = p.Id,
                    Title = p.Title,
                    Brand = p.Brand,
                    Sku = p.Sku,
                    CurrentPrimaryImage = p.ImageUrl,
                    ProposedPrimaryImage = p.ImageUrl,
                    CurrentGalleryCount = p.Images.Count,
                    Status = ImageMappingStatus.AdminUploadedPreserved,
                    StatusDetails = "Custom admin upload in /uploads/. Preserved untouched."
                });
                result.AdminUploadedPreservedCount++;
                continue;
            }

            // Verify if proposed catalog image exists physically on disk or via public site URL
            bool proposedAccessible = await VerifyImageAccessibleAsync(matchedCatalog.ImageUrl, ct);
            if (!proposedAccessible)
            {
                result.Items.Add(new ImageMappingPreviewItem
                {
                    ProductId = p.Id,
                    Title = p.Title,
                    Brand = p.Brand,
                    Sku = p.Sku,
                    CurrentPrimaryImage = p.ImageUrl,
                    ProposedPrimaryImage = matchedCatalog.ImageUrl,
                    CurrentGalleryCount = p.Images.Count,
                    Status = ImageMappingStatus.MissingOnDisk,
                    StatusDetails = $"Proposed image '{matchedCatalog.ImageUrl}' was not verified on server disk or public site. Preserved current."
                });
                result.MissingOnDiskCount++;
                continue;
            }

            bool primaryNeedsCorrection = IsGenericOrPlaceholderImage(p.ImageUrl) || !string.Equals(p.ImageUrl, matchedCatalog.ImageUrl, StringComparison.OrdinalIgnoreCase);

            var existingGalleryUrls = p.Images.Select(i => i.ImageUrl.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var proposedNewGalleryImages = new List<string>();

            if (matchedCatalog.AdditionalImages != null)
            {
                foreach (var addImg in matchedCatalog.AdditionalImages)
                {
                    if (!string.IsNullOrWhiteSpace(addImg) &&
                        !existingGalleryUrls.Contains(addImg.Trim()) &&
                        await VerifyImageAccessibleAsync(addImg, ct))
                    {
                        proposedNewGalleryImages.Add(addImg);
                    }
                }
            }

            if (primaryNeedsCorrection || proposedNewGalleryImages.Any())
            {
                result.Items.Add(new ImageMappingPreviewItem
                {
                    ProductId = p.Id,
                    Title = p.Title,
                    Brand = p.Brand,
                    Sku = p.Sku,
                    CurrentPrimaryImage = p.ImageUrl,
                    ProposedPrimaryImage = primaryNeedsCorrection ? matchedCatalog.ImageUrl : p.ImageUrl,
                    WillUpdatePrimary = primaryNeedsCorrection,
                    CurrentGalleryCount = p.Images.Count,
                    ProposedNewGalleryImages = proposedNewGalleryImages,
                    Status = ImageMappingStatus.RequiresCorrection,
                    StatusDetails = primaryNeedsCorrection 
                        ? $"Update primary from '{p.ImageUrl}' to genuine packaging. Add {proposedNewGalleryImages.Count} new gallery angles."
                        : $"Primary image correct. Add {proposedNewGalleryImages.Count} missing gallery angles without deleting existing."
                });
                result.RequiresCorrectionCount++;
            }
            else
            {
                result.Items.Add(new ImageMappingPreviewItem
                {
                    ProductId = p.Id,
                    Title = p.Title,
                    Brand = p.Brand,
                    Sku = p.Sku,
                    CurrentPrimaryImage = p.ImageUrl,
                    ProposedPrimaryImage = p.ImageUrl,
                    CurrentGalleryCount = p.Images.Count,
                    Status = ImageMappingStatus.AlreadyCorrect,
                    StatusDetails = "Genuine packaging image and gallery already perfectly mapped."
                });
                result.AlreadyCorrectCount++;
            }
        }

        result.Message = $"Image mapping preview complete: {result.RequiresCorrectionCount} products require image correction/gallery addition, {result.AlreadyCorrectCount} already correct, {result.AdminUploadedPreservedCount} admin uploads protected.";
        return result;
    }

    public async Task<ImageMappingExecutionResult> ExecuteImageMappingAsync(string executedByUserEmail, CancellationToken ct = default)
    {
        var result = new ImageMappingExecutionResult
        {
            ExecutedBy = executedByUserEmail
        };

        if (!_configuration.GetValue<bool>("CatalogImport:Enabled", true))
        {
            result.Success = false;
            result.Errors.Add("Image mapping is disabled by configuration (CatalogImport:Enabled=false).");
            return result;
        }

        var preview = await PreviewImageMappingAsync(ct);
        var toCorrect = preview.Items.Where(i => i.Status == ImageMappingStatus.RequiresCorrection).ToList();

        if (!toCorrect.Any())
        {
            result.Success = true;
            result.SkippedCount = preview.Items.Count;
            result.Details.Add("All existing products already possess genuine images and gallery angles. No corrections required.");
            return result;
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                int primaryUpdated = 0;
                int galleryAdded = 0;

                var productIds = toCorrect.Select(i => i.ProductId).ToList();
                var products = await _context.Products.Include(p => p.Images).Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
                var previewMap = toCorrect.ToDictionary(i => i.ProductId);

                foreach (var product in products)
                {
                    if (!previewMap.TryGetValue(product.Id, out var item)) continue;

                    // 1. Update primary image if required and safe
                    if (item.WillUpdatePrimary && !IsAdminUploadedImage(product.ImageUrl))
                    {
                        if (await VerifyImageAccessibleAsync(item.ProposedPrimaryImage, ct))
                        {
                            product.ImageUrl = item.ProposedPrimaryImage;
                            primaryUpdated++;

                            // Ensure primary exists in gallery without removing any existing rows
                            var existingPrimaryImg = product.Images.FirstOrDefault(i => i.IsPrimary);
                            if (existingPrimaryImg != null)
                            {
                                existingPrimaryImg.ImageUrl = item.ProposedPrimaryImage;
                            }
                            else
                            {
                                product.Images.Add(new ProductImage
                                {
                                    ImageUrl = item.ProposedPrimaryImage,
                                    IsPrimary = true,
                                    DisplayOrder = 1
                                });
                            }
                        }
                    }

                    // 2. Add missing gallery images without deleting any existing ones
                    if (item.ProposedNewGalleryImages.Any())
                    {
                        int maxOrder = product.Images.Any() ? product.Images.Max(i => i.DisplayOrder) : 1;
                        var existingUrls = product.Images.Select(i => i.ImageUrl.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

                        foreach (var newImg in item.ProposedNewGalleryImages)
                        {
                            if (!existingUrls.Contains(newImg.Trim()) && await VerifyImageAccessibleAsync(newImg, ct))
                            {
                                maxOrder++;
                                product.Images.Add(new ProductImage
                                {
                                    ImageUrl = newImg,
                                    IsPrimary = false,
                                    DisplayOrder = maxOrder
                                });
                                existingUrls.Add(newImg.Trim());
                                galleryAdded++;
                            }
                        }
                    }

                    result.Details.Add($"Product #{product.Id} ({product.Title}): Primary set to '{product.ImageUrl}', Gallery count now {product.Images.Count}.");
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                result.Success = true;
                result.UpdatedPrimaryCount = primaryUpdated;
                result.AddedGalleryImagesCount = galleryAdded;
                result.SkippedCount = preview.Items.Count - toCorrect.Count;

                await _auditService.LogAsync(
                    "ImageMapping",
                    "Product",
                    null,
                    $"Safely synchronized genuine photographs: updated {primaryUpdated} primary images and added {galleryAdded} gallery angle images. Zero existing ProductImage rows were deleted.",
                    executedByUserEmail);

                _logger.LogInformation("ImageMapping executed by {User}: Updated {Primary} primary images, added {Gallery} gallery angles.", executedByUserEmail, primaryUpdated, galleryAdded);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Transaction rolled back during ImageMapping execution.");
                result.Success = false;
                result.Errors.Add($"Image mapping transaction failed and was rolled back: {ex.Message}");
            }
        });

        return result;
    }

    private static async Task<(List<ProductJsonCatalogItem> Data, string? Path)> LoadCatalogJsonAsync(CancellationToken ct)
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "catalog_300plus.json"),
            Path.Combine(AppContext.BaseDirectory, "Data", "catalog_300plus.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "catalog_300plus.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "catalog_300plus.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "publish", "Web", "catalog_300plus.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Ayurveda.Infrastructure", "Data", "catalog_300plus.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Ayurveda.Infrastructure", "Data", "catalog_300plus.json")
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir != null; i++)
        {
            candidates.Add(Path.Combine(dir.FullName, "catalog_300plus.json"));
            candidates.Add(Path.Combine(dir.FullName, "src", "Ayurveda.Infrastructure", "Data", "catalog_300plus.json"));
            candidates.Add(Path.Combine(dir.FullName, "publish", "Web", "catalog_300plus.json"));
            dir = dir.Parent;
        }

        string? foundPath = candidates.Distinct().FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(foundPath))
        {
            return (new List<ProductJsonCatalogItem>(), null);
        }

        try
        {
            var content = await File.ReadAllTextAsync(foundPath, ct);
            var items = JsonSerializer.Deserialize<List<ProductJsonCatalogItem>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return (items ?? new List<ProductJsonCatalogItem>(), foundPath);
        }
        catch
        {
            return (new List<ProductJsonCatalogItem>(), foundPath);
        }
    }

    private async ValueTask<bool> VerifyImageAccessibleAsync(string? relativeUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return false;

        // 1. First probe local disk paths
        if (ImageExistsOnDisk(relativeUrl))
        {
            return true;
        }

        // 2. Check if optional configured physical path exists
        var staticAssetsPath = _configuration["CatalogImport:StaticAssetsPath"];
        if (!string.IsNullOrWhiteSpace(staticAssetsPath) && Directory.Exists(staticAssetsPath))
        {
            var trimmed = relativeUrl.TrimStart('~').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var combined = Path.Combine(staticAssetsPath, trimmed);
            if (File.Exists(combined))
            {
                return true;
            }
        }

        // 3. Fallback: Public Customer Website HTTP Probe (for Admin running independently on MonsterASP)
        var publicSiteUrl = _configuration["PublicSiteUrl"] ?? _configuration["CatalogImport:PublicSiteUrl"];
        if (string.IsNullOrWhiteSpace(publicSiteUrl))
        {
            return false;
        }

        // Strict security validation & SSRF prevention:
        // A. Must start with /images/ or images/
        var cleanUrl = relativeUrl.TrimStart('~');
        if (!cleanUrl.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) &&
            !cleanUrl.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejecting image verification for suspicious non-image path: {Url}", relativeUrl);
            return false;
        }

        // B. Must not contain directory traversal or illegal characters
        if (cleanUrl.Contains("..") || cleanUrl.Contains('\\') || cleanUrl.Contains(':'))
        {
            _logger.LogWarning("Rejecting image verification with traversal characters: {Url}", relativeUrl);
            return false;
        }

        if (!Uri.TryCreate(publicSiteUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (!Uri.TryCreate(new Uri(baseUri.ToString().TrimEnd('/') + "/"), cleanUrl.TrimStart('/'), out var targetUri))
        {
            return false;
        }

        // Enforce strict host matching to prevent SSRF
        if (!string.Equals(targetUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejecting image probe: target host '{TargetHost}' does not match PublicSiteUrl host '{BaseHost}'",
                targetUri.Host, baseUri.Host);
            return false;
        }

        var uriString = targetUri.AbsoluteUri;
        if (_verifiedUrlCache.TryGetValue(uriString, out bool cachedResult))
        {
            return cachedResult;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4));

            using var client = _httpClientFactory?.CreateClient("CatalogImport") ?? new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

            // Try HEAD first (fast, header-only)
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, targetUri);
            var response = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _verifiedUrlCache[uriString] = true;
                return true;
            }

            // Fallback to GET if server disallows HEAD (405 Method Not Allowed)
            if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, targetUri);
                var getResponse = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                bool success = getResponse.IsSuccessStatusCode;
                _verifiedUrlCache[uriString] = success;
                return success;
            }

            _verifiedUrlCache[uriString] = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HTTP image probe failed for {TargetUri}", targetUri);
            _verifiedUrlCache[uriString] = false;
            return false;
        }
    }

    private static bool ImageExistsOnDisk(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return false;
        var trimmed = relativeUrl.TrimStart('~').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var cur = Directory.GetCurrentDirectory();
        var baseDir = AppContext.BaseDirectory;

        var paths = new List<string>
        {
            Path.Combine(cur, "wwwroot", trimmed),
            Path.Combine(baseDir, "wwwroot", trimmed),
            Path.Combine(cur, "publish", "Web", "wwwroot", trimmed),
            Path.Combine(cur, "..", "Ayurveda.Web", "wwwroot", trimmed),
            Path.Combine(cur, "..", "Ayurveda.Admin", "wwwroot", trimmed),
            Path.Combine(cur, "src", "Ayurveda.Web", "wwwroot", trimmed),
            Path.Combine(cur, "src", "Ayurveda.Admin", "wwwroot", trimmed)
        };

        var dir = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && dir != null; i++)
        {
            paths.Add(Path.Combine(dir.FullName, "src", "Ayurveda.Web", "wwwroot", trimmed));
            paths.Add(Path.Combine(dir.FullName, "src", "Ayurveda.Admin", "wwwroot", trimmed));
            paths.Add(Path.Combine(dir.FullName, "publish", "Web", "wwwroot", trimmed));
            paths.Add(Path.Combine(dir.FullName, "wwwroot", trimmed));
            dir = dir.Parent;
        }

        return paths.Distinct().Any(File.Exists);
    }

    private static bool IsGenericOrPlaceholderImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return true;
        var lower = imageUrl.ToLowerInvariant();
        if (lower.Contains("placeholder")) return true;
        if (lower.EndsWith(".svg") && lower.Contains("/products/")) return true;
        return false;
    }

    private static bool IsAdminUploadedImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return false;
        var lower = imageUrl.ToLowerInvariant();
        return lower.StartsWith("/uploads/") || lower.Contains("/uploads/");
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        var s = title.ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]", "");
        return s;
    }

    private class ProductJsonCatalogItem
    {
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string SubcategoryName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Mrp { get; set; }
        public int Stock { get; set; }
        public double Rating { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string? Tags { get; set; }
        public string? TraditionallyUsedFor { get; set; }
        public string? Ingredients { get; set; }
        public string? HowToUse { get; set; }
        public string? Precautions { get; set; }
        public bool IsPrescriptionRequired { get; set; }
        public List<string> AdditionalImages { get; set; } = new();
        public List<ProductVariantItem> Variants { get; set; } = new();
        public List<ProductReviewItem> Reviews { get; set; } = new();
        public List<ProductFaqItem> Faqs { get; set; } = new();
    }

    private class ProductVariantItem
    {
        public string Title { get; set; } = string.Empty;
        public string SizeOrPack { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Mrp { get; set; }
        public int Stock { get; set; }
    }

    private class ProductReviewItem
    {
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Helpful { get; set; }
        public int DaysAgo { get; set; }
    }

    private class ProductFaqItem
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string AskedBy { get; set; } = string.Empty;
    }
}
