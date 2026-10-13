using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
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

// Register Domain & Infrastructure Services
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IPaymentProvider, PaymentProvider>();
builder.Services.AddScoped<ICallProvider, CallProvider>();
builder.Services.AddScoped<ITelephonyProvider, TelephonyProvider>();

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews();

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

        await DbInitializer.InitializeAsync(db, userManager, roleManager, logger);
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

app.UseHttpsRedirection();
app.UseStaticFiles();

// Serve shared uploads directory
var sharedUploadsDir = FileStorageService.ResolveSharedUploadsPath(builder.Configuration);
Directory.CreateDirectory(sharedUploadsDir);
var webExistingUploads = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), "uploads");
if (Directory.Exists(webExistingUploads) && webExistingUploads != sharedUploadsDir)
{
    foreach (var file in Directory.GetFiles(webExistingUploads))
    {
        var dest = Path.Combine(sharedUploadsDir, Path.GetFileName(file));
        if (!File.Exists(dest)) File.Copy(file, dest);
    }
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(sharedUploadsDir),
    RequestPath = "/uploads"
});

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

namespace Ayurveda.Web
{
    public partial class Program { }
}
