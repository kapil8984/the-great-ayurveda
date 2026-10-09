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
        options.UseSqlServer(connectionString);
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

// Role & Policy-Based Authorization for Administrative Segregation
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

// Bootstrap admin staff accounts if needed
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
            await db.Database.MigrateAsync();
        }

        await DbInitializer.InitializeAsync(db, userManager, roleManager, logger);

        // Ensure 4 administrative role accounts exist in dev/test
        var adminAccounts = new (string Email, string Name, UserRole Role)[]
        {
            ("admin@thegreatayurveda.com", "System SuperAdmin", UserRole.SuperAdmin),
            ("catalog@thegreatayurveda.com", "Catalog Manager", UserRole.CatalogManager),
            ("support@thegreatayurveda.com", "Support Agent", UserRole.SupportAgent),
            ("finance@thegreatayurveda.com", "Finance Manager", UserRole.FinanceManager)
        };

        foreach (var acc in adminAccounts)
        {
            var existingUser = await userManager.FindByEmailAsync(acc.Email);
            if (existingUser == null)
            {
                var newUser = new ApplicationUser
                {
                    UserName = acc.Email,
                    Email = acc.Email,
                    FullName = acc.Name,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var createResult = await userManager.CreateAsync(newUser, "AdminSecure@2026");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(newUser, acc.Role.ToString());
                    logger.LogInformation("Administrative account created: {Email} ({Role})", acc.Email, acc.Role);
                }
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize Admin database.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Auth/Login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

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
