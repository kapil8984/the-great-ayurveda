using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;
using Ayurveda.Infrastructure.Identity;
using Ayurveda.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Rolling File Logger in App_Data/logs (7-day retention)
var logsDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "logs");
builder.Logging.AddProvider(new RollingFileLoggerProvider(logsDir));

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

// Cookie Configuration for Public Site
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Ayurveda.Web.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Dedicated Admin Cookie Scheme (Isolated from public site)
builder.Services.AddAuthentication()
    .AddCookie("AdminCookieScheme", options =>
    {
        options.Cookie.Name = ".Ayurveda.Admin.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/admin-portal/auth/login";
        options.AccessDeniedPath = "/admin-portal/auth/accessdenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Admin Authorization Policies with legacy "Admin" role mapped to SuperAdmin
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireSuperAdmin", policy =>
    {
        policy.AddAuthenticationSchemes("AdminCookieScheme");
        policy.RequireRole(UserRole.SuperAdmin.ToString(), "Admin", "DemoViewer");
    });

    options.AddPolicy("RequireCatalogManagement", policy =>
    {
        policy.AddAuthenticationSchemes("AdminCookieScheme");
        policy.RequireRole(UserRole.SuperAdmin.ToString(), "Admin", UserRole.CatalogManager.ToString(), "DemoViewer");
    });

    options.AddPolicy("RequireFinanceManagement", policy =>
    {
        policy.AddAuthenticationSchemes("AdminCookieScheme");
        policy.RequireRole(UserRole.SuperAdmin.ToString(), "Admin", UserRole.FinanceManager.ToString(), "DemoViewer");
    });

    options.AddPolicy("RequireCustomerSupport", policy =>
    {
        policy.AddAuthenticationSchemes("AdminCookieScheme");
        policy.RequireRole(UserRole.SuperAdmin.ToString(), "Admin", UserRole.SupportAgent.ToString(), "DemoViewer");
    });
});

// Register Domain & Infrastructure Services
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPaymentProvider, PaymentProvider>();
builder.Services.AddScoped<ICallProvider, CallProvider>();
builder.Services.AddScoped<ITelephonyProvider, TelephonyProvider>();
builder.Services.AddScoped<ICacheBuster, CacheBuster>();

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = false;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<Ayurveda.Web.Filters.DemoViewerReadOnlyFilter>();
});

builder.Services.AddHostedService<Ayurveda.Web.Services.WorkingSetTrimmerService>();

#if DEBUG
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "The Great Ayurveda API",
        Version = "v1",
        Description = "Development JSON API documentation for The Great Ayurveda endpoints."
    });
});
#endif

var app = builder.Build();

// Seed initial data & roles (Web is the sole runner of migrations & seed)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<AyurvedaDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();

        if (db.Database.IsRelational())
        {
            var canConnect = false;
            try
            {
                canConnect = await db.Database.CanConnectAsync();
            }
            catch
            {
                canConnect = false;
            }

            if (canConnect)
            {
                try
                {
                    using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT 
                            (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME <> '__EFMigrationsHistory') AS UserTables,
                            (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME = '__EFMigrationsHistory') AS HistoryTable;
                    ";
                    int userTables = 0;
                    int historyTable = 0;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            userTables = reader.GetInt32(0);
                            historyTable = reader.GetInt32(1);
                        }
                    }

                    bool historyMissingOrEmpty = false;
                    if (historyTable == 0)
                    {
                        historyMissingOrEmpty = true;
                    }
                    else
                    {
                        using var countCmd = conn.CreateCommand();
                        countCmd.CommandText = "SELECT COUNT(*) FROM [__EFMigrationsHistory]";
                        var countObj = await countCmd.ExecuteScalarAsync();
                        var count = countObj != null ? Convert.ToInt32(countObj) : 0;
                        if (count == 0)
                        {
                            historyMissingOrEmpty = true;
                        }
                    }

                    if (userTables > 0 && historyMissingOrEmpty)
                    {
                        logger.LogCritical("FATAL: Database schema mismatch detected. Database contains {TableCount} tables, but __EFMigrationsHistory is missing or empty. Automatic drop and recreate is disabled to prevent accidental data loss. To recreate and reseed the database, run './reset-db.ps1'. Startup aborted.", userTables);
                        throw new InvalidOperationException($"Existing database tables detected ({userTables} tables) without __EFMigrationsHistory. Automatic drop is disabled; run reset-db.ps1 to cleanly reset the database.");
                    }
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    logger.LogWarning(ex, "Pre-migration database verification check encountered an issue: {Message}", ex.Message);
                }
            }

            try
            {
                logger.LogInformation("Applying EF Core migrations from Ayurveda.Infrastructure...");
                await db.Database.MigrateAsync();
                logger.LogInformation("EF Core migrations applied successfully.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "FATAL: Database migration failed. Schema mismatch or migration conflict detected: {Message}. Automatic drop is disabled to protect data integrity. Run './reset-db.ps1' if a fresh database is required. Aborting application startup.", ex.Message);
                throw;
            }
        }

        await DbInitializer.InitializeAsync(db, userManager, roleManager, logger, builder.Configuration, builder.Environment);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "FATAL: Database initialization or migration failed. Aborting application startup: {Message}", ex.Message);
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
#if DEBUG
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "The Great Ayurveda API v1");
        c.RoutePrefix = "swagger";
    });
}
#endif

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 2
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
forwardedHeadersOptions.KnownProxies.Add(System.Net.IPAddress.Loopback);
forwardedHeadersOptions.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);

var additionalProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
if (additionalProxies != null)
{
    foreach (var p in additionalProxies)
    {
        if (System.Net.IPAddress.TryParse(p, out var parsedIp))
            forwardedHeadersOptions.KnownProxies.Add(parsedIp);
    }
}
app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/home/notfound");

// 301 Permanent Redirect for uppercase route paths to enforce strict lowercase canonical URLs
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
        !string.IsNullOrEmpty(path) &&
        !path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/admin-portal", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) &&
        path.Any(char.IsUpper))
    {
        var lowercaseUrl = path.ToLowerInvariant() + (context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "");
        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = lowercaseUrl;
        return;
    }
    await next();
});

app.UseStaticFiles();

// Fixed-Window Rate Limiting Middleware for Auth and Search endpoints
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
    var method = context.Request.Method;

    var isAuthPost = HttpMethods.IsPost(method) && (
        path.StartsWith("/account/login") ||
        path.StartsWith("/doctor/login") ||
        path.StartsWith("/admin-portal/login") ||
        path.StartsWith("/admin-portal/auth/login") ||
        path.StartsWith("/account/register") ||
        path.StartsWith("/account/doctor-register") ||
        path.StartsWith("/account/forgot-password"));

    var isSearchSuggest = HttpMethods.IsGet(method) && path.StartsWith("/search/suggest");

    if (isAuthPost || isSearchSuggest)
    {
        var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var trustForwardedFor = configuration.GetValue<bool>("RateLimit:TrustForwardedFor", false);

        string ip = "unknown";
        if (trustForwardedFor)
        {
            var fwd = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fwd))
            {
                ip = fwd.Split(',')[0].Trim();
            }
        }
        if (ip == "unknown")
        {
            ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        var searchLimit = configuration.GetValue<int?>("RateLimit:SearchSuggestLimit") ?? 60;
        var limit = isAuthPost ? 15 : searchLimit;
        var prefix = isAuthPost ? "rl_auth_" : "rl_suggest_";
        var key = $"{prefix}{ip}_{path}";

        var current = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return 0;
        });

        if (current >= limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Rate limit exceeded. Too many requests. Please try again after 1 minute.");
            return;
        }

        cache.Set(key, current + 1, TimeSpan.FromMinutes(1));
    }

    await next();
});

// Ensure wwwroot/uploads directory exists for shared upload assets
var uploadsDir = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), "uploads");
Directory.CreateDirectory(uploadsDir);

// Security Headers & Admin Host Isolation
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    if (context.Request.Path.StartsWithSegments("/admin-portal"))
    {
        context.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow");
    }
    await next();
});

app.UseRouting();
app.UseSession();

app.UseAuthentication();

// Set context.User from AdminCookieScheme for all /admin-portal paths
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/admin-portal"))
    {
        var adminResult = await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.AuthenticateAsync(context, "AdminCookieScheme");
        if (adminResult.Succeeded && adminResult.Principal != null)
        {
            context.User = adminResult.Principal;
        }
    }
    await next();
});

app.UseAuthorization();

// Dedicated Admin Portal Area Route
app.MapAreaControllerRoute(
    name: "AdminArea",
    areaName: "Admin",
    pattern: "admin-portal/{controller=Dashboard}/{action=Index}/{id?}");

// robots.txt & sitemap.xml using configured Site:BaseUrl (no hardcoded domain)
app.MapGet("/robots.txt", (IConfiguration config, HttpContext httpContext) =>
{
    var baseUrl = (config["Site:BaseUrl"] ?? config["SiteSettings:BaseUrl"] ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}").TrimEnd('/');
    var content = $"User-agent: *\nDisallow: /admin-portal/\nDisallow: /admin-portal\nDisallow: /Admin-Portal/\nDisallow: /Admin-Portal\nDisallow: /account/\nDisallow: /account\nDisallow: /Account/\nDisallow: /Account\nDisallow: /checkout/\nDisallow: /checkout\nDisallow: /Checkout/\nDisallow: /Checkout\nDisallow: /cart/\nDisallow: /cart\nDisallow: /Cart/\nDisallow: /Cart\nDisallow: /doctorportal/\nDisallow: /doctorportal\nDisallow: /DoctorPortal/\nDisallow: /DoctorPortal\nDisallow: /doctor-portal/\nDisallow: /doctor-portal\nDisallow: /Doctor-Portal/\nDisallow: /Doctor-Portal\n\nSitemap: {baseUrl}/sitemap.xml\n";
    return Results.Text(content, "text/plain");
});

app.MapGet("/sitemap.xml", async (AyurvedaDbContext db, IConfiguration config, HttpContext httpContext) =>
{
    var baseUrl = (config["Site:BaseUrl"] ?? config["SiteSettings:BaseUrl"] ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}").TrimEnd('/');
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

    var staticPages = new[] { "", "/shop", "/doctors", "/diseases", "/home/about", "/home/contact", "/home/faq", "/home/privacy" };
    foreach (var page in staticPages)
    {
        sb.AppendLine($"  <url><loc>{baseUrl}{page}</loc><changefreq>weekly</changefreq><priority>{(page == "" ? "1.0" : "0.8")}</priority></url>");
    }

    var products = await db.Products.Where(p => p.IsActive).Select(p => p.Id).ToListAsync();
    foreach (var pId in products)
    {
        sb.AppendLine($"  <url><loc>{baseUrl}/shop/details/{pId}</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>");
    }

    var doctors = await db.Doctors.Where(d => !d.IsDeleted && d.DoctorDetail != null && d.DoctorDetail.Status == 1).Select(d => d.Id).ToListAsync();
    foreach (var dId in doctors)
    {
        sb.AppendLine($"  <url><loc>{baseUrl}/doctors/profile/{dId}</loc><changefreq>weekly</changefreq><priority>0.6</priority></url>");
    }

    sb.AppendLine("</urlset>");
    return Results.Content(sb.ToString(), "application/xml", System.Text.Encoding.UTF8);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

namespace Ayurveda.Web
{
    public partial class Program { }
}
