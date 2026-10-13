using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly ILogger<FileStorageService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _sharedUploadsDir;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    private static readonly Dictionary<string, byte[]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/jpg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        { "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 } }
    };

    public FileStorageService(ILogger<FileStorageService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _sharedUploadsDir = ResolveSharedUploadsPath(configuration);
        if (!Directory.Exists(_sharedUploadsDir))
        {
            Directory.CreateDirectory(_sharedUploadsDir);
        }
    }

    public static string ResolveSharedUploadsPath(IConfiguration configuration, string? contentRootPath = null, string? webRootPath = null)
    {
        var configPath = configuration["Storage:UploadsPath"];
        if (!string.IsNullOrWhiteSpace(configPath) && Path.IsPathRooted(configPath))
            return Path.GetFullPath(configPath);

        var baseDir = !string.IsNullOrWhiteSpace(webRootPath)
            ? webRootPath
            : (!string.IsNullOrWhiteSpace(contentRootPath)
                ? Path.Combine(contentRootPath, "wwwroot")
                : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));

        var relativeFolder = !string.IsNullOrWhiteSpace(configPath) ? configPath : "uploads";
        if (relativeFolder.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase) || relativeFolder.StartsWith("wwwroot\\", StringComparison.OrdinalIgnoreCase))
        {
            relativeFolder = relativeFolder.Substring(8);
        }
        else if (relativeFolder.StartsWith("shared/uploads", StringComparison.OrdinalIgnoreCase) || relativeFolder.StartsWith("shared\\uploads", StringComparison.OrdinalIgnoreCase))
        {
            relativeFolder = "uploads";
        }

        return Path.GetFullPath(Path.Combine(baseDir, relativeFolder));
    }

    public async Task<string> SaveFileAsync(Stream stream, string originalFileName, string contentType)
    {
        if (stream == null || stream.Length == 0)
            throw new ArgumentException("File stream is empty.");

        if (stream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("File size exceeds 5MB limit.");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".svg" };
        if (!allowedExtensions.Contains(ext))
            throw new InvalidOperationException("Invalid file extension.");

        // Check magic bytes for images if content type is specified
        if (!string.IsNullOrEmpty(contentType) && MagicBytes.TryGetValue(contentType, out var expectedBytes))
        {
            var header = new byte[expectedBytes.Length];
            var originalPos = stream.Position;
            var read = await stream.ReadAsync(header, 0, header.Length);
            stream.Position = originalPos;

            if (read < expectedBytes.Length || !header.SequenceEqual(expectedBytes))
                throw new InvalidOperationException("File content magic bytes do not match declared content type.");
        }

        Directory.CreateDirectory(_sharedUploadsDir);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_sharedUploadsDir, safeFileName);

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

        var fileName = Path.GetFileName(relativeFilePath);
        var fullPath = Path.Combine(_sharedUploadsDir, fileName);

        if (File.Exists(fullPath))
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
