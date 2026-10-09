using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly ILogger<FileStorageService> _logger;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    private static readonly Dictionary<string, byte[]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/jpg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        { "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 } }
    };

    public FileStorageService(ILogger<FileStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(Stream stream, string originalFileName, string contentType)
    {
        if (stream == null || stream.Length == 0)
            throw new ArgumentException("File stream is empty.");

        if (stream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("File size exceeds 5MB limit.");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
        if (!allowedExtensions.Contains(ext))
            throw new InvalidOperationException("Invalid file extension.");

        // Check magic bytes for images
        if (MagicBytes.TryGetValue(contentType, out var expectedBytes))
        {
            var header = new byte[expectedBytes.Length];
            var originalPos = stream.Position;
            var read = await stream.ReadAsync(header, 0, header.Length);
            stream.Position = originalPos;

            if (read < expectedBytes.Length || !header.SequenceEqual(expectedBytes))
                throw new InvalidOperationException("File content magic bytes do not match declared content type.");
        }

        var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsDir, safeFileName);

        using (var dest = new FileStream(fullPath, FileMode.Create))
        {
            await stream.CopyToAsync(dest);
        }

        _logger.LogInformation("Stored uploaded file securely at {FullPath}", fullPath);
        return $"/uploads/{safeFileName}";
    }

    public void DeleteFile(string relativeFilePath)
    {
        if (string.IsNullOrWhiteSpace(relativeFilePath)) return;

        var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullPath = Path.GetFullPath(Path.Combine(webRoot, relativeFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

        if (fullPath.StartsWith(webRoot) && File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file at {FullPath}", fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file at {FullPath}", fullPath);
            }
        }
    }
}
