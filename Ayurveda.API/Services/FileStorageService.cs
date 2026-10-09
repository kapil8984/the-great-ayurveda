using System.Security.Cryptography;

namespace Ayurveda.API.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string subDirectory = "images");
    bool DeleteFile(string filename, string subDirectory = "images");
}

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileStorageService> _logger;

    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB limit

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(IFormFile file, string subDirectory = "images")
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is empty or not provided.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new ArgumentException($"File size exceeds maximum allowed limit of {MaxFileSize / (1024 * 1024)}MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"File extension '{extension}' is not supported. Allowed formats: JPG, PNG, WEBP.");
        }

        if (string.IsNullOrEmpty(file.ContentType) || !AllowedMimeTypes.Contains(file.ContentType))
        {
            throw new ArgumentException($"Content-Type '{file.ContentType}' is not permitted. Only standard image types are allowed.");
        }

        // Validate file magic bytes
        byte[] headerBytes = new byte[12];
        using (var stream = file.OpenReadStream())
        {
            var bytesRead = await stream.ReadAsync(headerBytes, 0, headerBytes.Length);
            if (bytesRead < 4 || !IsValidImageMagicBytes(headerBytes, extension))
            {
                throw new ArgumentException("File content signature does not match the stated image format (invalid magic bytes).");
            }
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var sanitizedSubDir = Path.GetFileName(subDirectory); // Prevent directory traversal
        var uploadsFolder = Path.Combine(webRoot, sanitizedSubDir);

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        // Generate cryptographically secure random filename
        var randomToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var uniqueFileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{randomToken}{extension}";
        var destinationPath = Path.GetFullPath(Path.Combine(uploadsFolder, uniqueFileName));

        // Path traversal sanity check
        if (!destinationPath.StartsWith(Path.GetFullPath(uploadsFolder), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path target.");
        }

        using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(destinationStream);
        }

        _logger.LogInformation("Secure image file saved: {FileName}", uniqueFileName);
        return uniqueFileName;
    }

    public bool DeleteFile(string filename, string subDirectory = "images")
    {
        if (string.IsNullOrWhiteSpace(filename)) return false;

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var sanitizedSubDir = Path.GetFileName(subDirectory);
        var safeFileName = Path.GetFileName(filename);
        var filePath = Path.GetFullPath(Path.Combine(webRoot, sanitizedSubDir, safeFileName));

        if (!filePath.StartsWith(Path.GetFullPath(Path.Combine(webRoot, sanitizedSubDir)), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }

        return false;
    }

    private static bool IsValidImageMagicBytes(byte[] header, string extension)
    {
        // JPEG: FF D8 FF
        if (extension is ".jpg" or ".jpeg")
        {
            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (extension == ".png")
        {
            return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        }

        // WEBP: RIFF .... WEBP
        if (extension == ".webp")
        {
            return header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
        }

        return false;
    }
}
