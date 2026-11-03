using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;
using Ayurveda.Infrastructure.Identity;
using Ayurveda.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Data Protection Keys Persistence
var dataProtectionConfig = builder.Configuration["DataProtection:KeyPath"]
    ?? builder.Configuration["DataProtection:KeysPath"]
    ?? "App_Data/DataProtectionKeys";
var dataProtectionPath = Path.IsPathRooted(dataProtectionConfig)
    ? dataProtectionConfig
    : Path.Combine(builder.Environment.ContentRootPath, dataProtectionConfig);
Directory.CreateDirectory(dataProtectionPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("TheGreatAyurveda");

// Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<AyurvedaDbContext>((_, options) =>
{
    if (!options.IsConfigured)
    {
        options.UseSqlServer(connectionString, b => b.MigrationsAssembly("Ayurveda.Infrastructure"));
    }
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AyurvedaDbContext>()
.AddDefaultTokenProviders();

// Legacy BCrypt support & upgrade
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BCryptPasswordHasher>();

// Dedicated Admin Cookie Authentication (Independent from Public site)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Ayurveda.Admin.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Auth/Login";
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireSuperAdmin", policy =>
        policy.RequireRole(UserRole.SuperAdmin.ToString()));

    options.AddPolicy("RequireCatalogManagement", policy =>
        policy.RequireRole(UserRole.SuperAdmin.ToString(), UserRole.CatalogManager.ToString()));

    options.AddPolicy("RequireFinanceManagement", policy =>
        policy.RequireRole(UserRole.SuperAdmin.ToString(), UserRole.FinanceManager.ToString()));

    options.AddPolicy("RequireCustomerSupport", policy =>
        policy.RequireRole(UserRole.SuperAdmin.ToString(), UserRole.SupportAgent.ToString()));
});

builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICatalogImportService, CatalogImportService>();
builder.Services.AddSingleton<ITimezoneService, TimezoneService>();
builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddControllersWithViews();

var app = builder.Build();

var isDbReady = false;

// Connect to database initialized by Ayurveda.Web (Admin does NOT run migrations or seed)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<AyurvedaDbContext>();
    
    if (db.Database.IsRelational())
    {
        logger.LogInformation("Verifying database connection for Ayurveda.Admin (migrations and seeding are handled by Ayurveda.Web)...");
        const int maxRetries = 15;
        for (int retry = 1; retry <= maxRetries; retry++)
        {
            try
            {
                if (await db.Database.CanConnectAsync())
                {
                    using var conn = db.Database.GetDbConnection();
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUsers'";
                    var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    if (tableCount > 0)
                    {
                        logger.LogInformation("Database connection verified successfully. Ayurveda.Admin ready.");
                        isDbReady = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("Waiting for database initialization... (Attempt {Retry}/{Max}): {Message}", retry, maxRetries, ex.Message);
            }

            logger.LogInformation("Database not ready yet, waiting for Ayurveda.Web to initialize... (Attempt {Retry}/{Max})", retry, maxRetries);
            await Task.Delay(2000);
        }

        if (!isDbReady)
        {
            logger.LogWarning("Warning: Database was not initialized within timeout. Ensure Ayurveda.Web is run first to perform database migration and seeding.");
        }
    }
    else
    {
        isDbReady = true;
    }
}

// Friendly "Service is starting" middleware when database is empty (Requirement 8)
app.Use(async (context, next) =>
{
    if (!isDbReady)
    {
        try
        {
            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            if (await db.Database.CanConnectAsync())
            {
                using var conn = db.Database.GetDbConnection();
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUsers'";
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count > 0)
                {
                    isDbReady = true;
                }
            }
        }
        catch { }

        if (!isDbReady)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.Append("Retry-After", "5");
            await context.Response.WriteAsync(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <meta http-equiv=""refresh"" content=""5"" />
    <title>Service Initializing • The Great Ayurveda Admin</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0A160F; color: #E8F5E9; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; padding: 1.5rem; text-align: center; }
        .card { background: rgba(19, 42, 29, 0.85); border: 1px solid rgba(212, 175, 55, 0.3); border-radius: 1rem; padding: 2.5rem 2rem; max-width: 520px; box-shadow: 0 20px 40px rgba(0,0,0,0.5); }
        .spinner { width: 48px; height: 48px; border: 4px solid rgba(212, 175, 55, 0.2); border-top-color: #D4AF37; border-radius: 50%; animation: spin 1s linear infinite; margin: 0 auto 1.5rem auto; }
        h1 { font-size: 1.5rem; color: #D4AF37; margin: 0 0 1rem 0; font-weight: 600; }
        p { color: #A5D6A7; font-size: 0.95rem; line-height: 1.6; margin: 0 0 1.25rem 0; }
        .badge { display: inline-block; background: rgba(212, 175, 55, 0.15); color: #F3E5AB; padding: 0.35rem 0.85rem; border-radius: 2rem; font-size: 0.8rem; font-family: monospace; }
        @keyframes spin { to { transform: rotate(360deg); } }
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""spinner""></div>
        <h1>Service is Starting</h1>
        <p>The Great Ayurveda Admin Portal is waiting for initial database schema migration and data seeding to complete.</p>
        <p>Please ensure <strong>Ayurveda.Web</strong> has started and initialized the database.</p>
        <div class=""badge"">Auto-refreshing in 5 seconds...</div>
    </div>
</body>
</html>");
            return;
        }
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Auth/Login");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseHttpsRedirection();
app.UseStaticFiles();

// Serve shared assets from Ayurveda.Web wwwroot (images) and shared uploads
var sharedUploadsDir = FileStorageService.ResolveSharedUploadsPath(builder.Configuration, builder.Environment.ContentRootPath, builder.Environment.WebRootPath);
Directory.CreateDirectory(sharedUploadsDir);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(sharedUploadsDir),
    RequestPath = "/uploads"
});

var webWwwRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Ayurveda.Web", "wwwroot"));
if (Directory.Exists(webWwwRoot))
{
    var imagesPath = Path.Combine(webWwwRoot, "images");
    if (Directory.Exists(imagesPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(imagesPath),
            RequestPath = "/images"
        });
    }
}

// Security Headers & Host Isolation
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow");
    await next();
});

// Optional IP Allow-listing Middleware
app.Use(async (context, next) =>
{
    var allowedIpsConfig = app.Configuration.GetSection("AdminSecurity:AllowedIPs").Get<string[]>();
    if (allowedIpsConfig != null && allowedIpsConfig.Length > 0)
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(remoteIp) && !remoteIp.Equals("::1") && !remoteIp.Equals("127.0.0.1"))
        {
            if (!allowedIpsConfig.Contains(remoteIp))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden: IP address not authorized to access administrative console.");
                return;
            }
        }
    }
    await next();
});

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

namespace Ayurveda.Admin
{
    public partial class Program { }
}
