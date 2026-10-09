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

builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISearchService, SearchService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Bootstrap super admin account if needed
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

        // Ensure super admin exists in dev
        var adminEmail = "admin@thegreatayurveda.com";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System SuperAdmin",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(adminUser, "AdminSecure@2026");
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, UserRole.SuperAdmin.ToString());
                logger.LogInformation("Default development administrator created: {AdminEmail}", adminEmail);
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

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseRouting();

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
