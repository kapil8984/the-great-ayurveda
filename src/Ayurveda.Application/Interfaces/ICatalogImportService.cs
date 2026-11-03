using Ayurveda.Application.ViewModels;

namespace Ayurveda.Application.Interfaces;

public interface ICatalogImportService
{
    /// <summary>
    /// Returns current product counts, JSON status, and missing product telemetry.
    /// </summary>
    Task<CatalogSystemStatusViewModel> GetSystemStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Previews which products will be inserted and which already exist. Read-only: never modifies the database.
    /// </summary>
    Task<CatalogImportPreviewResult> PreviewProductImportAsync(CancellationToken ct = default);

    /// <summary>
    /// Safely imports missing products inside a database transaction.
    /// Never deletes products, never overwrites stock/prices/descriptions of existing products.
    /// </summary>
    Task<CatalogImportExecutionResult> ExecuteProductImportAsync(string executedByUserEmail, CancellationToken ct = default);

    /// <summary>
    /// Previews proposed image corrections for existing products.
    /// Validates disk presence, preserves admin /uploads/, never deletes ProductImage rows.
    /// Read-only: never modifies the database.
    /// </summary>
    Task<ImageMappingPreviewResult> PreviewImageMappingAsync(CancellationToken ct = default);

    /// <summary>
    /// Safely updates confirmed generic/placeholder image paths to genuine catalog images
    /// and adds missing gallery angles inside a database transaction.
    /// Never deletes existing ProductImage rows, preserves admin uploads, verifies disk presence.
    /// </summary>
    Task<ImageMappingExecutionResult> ExecuteImageMappingAsync(string executedByUserEmail, CancellationToken ct = default);
}

