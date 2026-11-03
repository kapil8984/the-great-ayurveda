using Ayurveda.Domain.Entities;

namespace Ayurveda.Application.ViewModels;

public class CatalogSystemStatusViewModel
{
    public int DatabaseProductCount { get; set; }
    public int CatalogJsonProductCount { get; set; }
    public int MissingProductsCount { get; set; }
    public int GenericOrPlaceholderImageCount { get; set; }
    public int ValidProductImageCount { get; set; }
    public int TotalProductImagesRows { get; set; }
    public string CatalogJsonPath { get; set; } = string.Empty;
    public bool CatalogJsonExists { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class CatalogImportProductItem
{
    public string Title { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string SubcategoryName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int AdditionalImagesCount { get; set; }
    public int? ExistingProductId { get; set; }
    public string MatchReason { get; set; } = string.Empty;
}

public class CatalogImportAmbiguousItem
{
    public string Title { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<int> ConflictingProductIds { get; set; } = new();
}

public class CatalogImportPreviewResult
{
    public int TotalCatalogItems { get; set; }
    public int ExistingMatchingCount { get; set; }
    public int NewProductsToInsertCount { get; set; }
    public int AmbiguousMatchesCount { get; set; }
    public List<CatalogImportProductItem> NewProducts { get; set; } = new();
    public List<CatalogImportProductItem> ExistingMatched { get; set; } = new();
    public List<CatalogImportAmbiguousItem> AmbiguousItems { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class CatalogImportExecutionResult
{
    public bool Success { get; set; }
    public int InsertedCount { get; set; }
    public int SkippedCount { get; set; }
    public int AmbiguousCount { get; set; }
    public List<string> InsertedProductTitles { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string ExecutedBy { get; set; } = string.Empty;
}

public enum ImageMappingStatus
{
    RequiresCorrection,
    AlreadyCorrect,
    AdminUploadedPreserved,
    MissingOnDisk,
    Ambiguous,
    ProductNotFoundInCatalog
}

public class ImageMappingPreviewItem
{
    public int ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string CurrentPrimaryImage { get; set; } = string.Empty;
    public string ProposedPrimaryImage { get; set; } = string.Empty;
    public int CurrentGalleryCount { get; set; }
    public List<string> ProposedNewGalleryImages { get; set; } = new();
    public ImageMappingStatus Status { get; set; }
    public string StatusDetails { get; set; } = string.Empty;
    public bool WillUpdatePrimary { get; set; }
    public int NewGalleryImagesCount => ProposedNewGalleryImages.Count;
}

public class ImageMappingPreviewResult
{
    public int TotalCatalogItems { get; set; }
    public int TotalDatabaseProducts { get; set; }
    public int ExistingMatchingCount { get; set; }
    public int AlreadyCorrectCount { get; set; }
    public int RequiresCorrectionCount { get; set; }
    public int AdminUploadedPreservedCount { get; set; }
    public int MissingOnDiskCount { get; set; }
    public int AmbiguousMatchesCount { get; set; }
    public List<ImageMappingPreviewItem> Items { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class ImageMappingExecutionResult
{
    public bool Success { get; set; }
    public int UpdatedPrimaryCount { get; set; }
    public int AddedGalleryImagesCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Details { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string ExecutedBy { get; set; } = string.Empty;
}

