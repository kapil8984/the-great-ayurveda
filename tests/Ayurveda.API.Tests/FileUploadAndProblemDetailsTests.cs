using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ayurveda.API.Middleware;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ayurveda.API.Tests;

public class FileUploadAndProblemDetailsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FileUploadAndProblemDetailsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FileStorageService_InvalidExtension_ThrowsArgumentException()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        // Attempt uploading .exe or .sh script
        var content = "echo evil"u8.ToArray();
        using var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", "malware.exe")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/x-msdownload"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveFileAsync(formFile));
        Assert.Contains("not supported", ex.Message);
    }

    [Fact]
    public async Task FileStorageService_ForgedExtensionWithInvalidMagicBytes_ThrowsArgumentException()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        // File claims to be .jpg and image/jpeg, but body is plaintext PHP/shell code (magic bytes do not start with FF D8 FF)
        var content = "<?php phpinfo(); ?>"u8.ToArray();
        using var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", "avatar.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveFileAsync(formFile));
        Assert.Contains("magic bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FileStorageService_ValidJpegWithProperMagicBytes_Succeeds()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        // Valid JPEG header: FF D8 FF E0 followed by dummy bytes
        byte[] validJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
        using var stream = new MemoryStream(validJpeg);
        var formFile = new FormFile(stream, 0, validJpeg.Length, "file", "valid.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var savedName = await service.SaveFileAsync(formFile);
        Assert.NotNull(savedName);
        Assert.EndsWith(".jpg", savedName);

        // Cleanup
        service.DeleteFile(savedName);
    }

    [Fact]
    public async Task ExceptionMiddleware_Unhandled500Exception_ReturnsRfc7807ProblemDetailsWithoutRawDetails()
    {
        var loggerMock = new LoggerFactory().CreateLogger<ExceptionMiddleware>();
        var middleware = new ExceptionMiddleware(
            next: (ctx) => throw new Exception("Secret internal database password exposed: S3cret!"),
            logger: loggerMock
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        // Must NOT leak internal error message
        Assert.DoesNotContain("S3cret", responseBody);
        Assert.DoesNotContain("database password", responseBody);

        // Must contain RFC 7807 fields
        using var jsonDoc = JsonDocument.Parse(responseBody);
        var root = jsonDoc.RootElement;
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("Internal Server Error", root.GetProperty("title").GetString());
        Assert.Contains("unexpected error", root.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }
}
