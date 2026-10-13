using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;
using Ayurveda.Infrastructure.Identity;
using Ayurveda.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

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
        var connected = false;
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
                        connected = true;
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

        if (!connected)
        {
            logger.LogWarning("Warning: Database was not initialized within timeout. Ensure Ayurveda.Web is run first to perform database migration and seeding.");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Auth/Login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Serve shared assets from Ayurveda.Web wwwroot (images) and shared uploads
var sharedUploadsDir = FileStorageService.ResolveSharedUploadsPath(builder.Configuration);
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
