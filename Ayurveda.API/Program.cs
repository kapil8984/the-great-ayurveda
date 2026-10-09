using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Ayurveda.API.Data;
using Ayurveda.API.Middleware;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Connection
var connectionString = builder.Configuration.GetConnectionString("AyurvedaDbConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<AyurvedaDbContext>(options =>
{
    if (!options.IsConfigured)
    {
        options.UseSqlServer(connectionString);
    }
});

// 2. JWT Configuration & Startup Validation (Rule d)
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Fatal: 'Jwt:Key' is missing or smaller than 32 bytes (256 bits). Configure a secure key in user-secrets or environment variables.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TheGreatAyurveda";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TheGreatAyurvedaApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
});

builder.Services.AddAuthorization();

// 3. Dependency Injection Services
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IYogaService, YogaService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// 4. Controllers & JSON Serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 5. CORS Restricted to Configured Origins (Rule f)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "https://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "The Great Ayurveda API",
        Version = "v1",
        Description = "ASP.NET Core .NET 10 Web API for The Great Ayurveda - Sacred Healthcare & Herbal Wellness"
    });

    var securityScheme = new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Enter JWT Bearer token: Bearer {your_jwt_token}"
    };

    options.AddSecurityDefinition("Bearer", securityScheme);

    var schemeReference = new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer");

    options.AddSecurityRequirement(_ => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            schemeReference,
            new List<string>()
        }
    });
});

var app = builder.Build();

// CLI Command: dotnet run -- seed-admin --email ... --password ... (Rule b)
if (args.Length > 0 && args[0].Equals("seed-admin", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var dbContext = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
    await dbContext.Database.MigrateAsync();

    string email = "";
    string password = "";
    string name = "System Administrator";
    string contact = "9999999999";

    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--email" && i + 1 < args.Length) email = args[++i];
        else if (args[i] == "--password" && i + 1 < args.Length) password = args[++i];
        else if (args[i] == "--name" && i + 1 < args.Length) name = args[++i];
        else if (args[i] == "--contact" && i + 1 < args.Length) contact = args[++i];
    }

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        Console.Error.WriteLine("Error: Both --email and --password are required to seed an admin.");
        return;
    }

    var created = await authService.SeedAdminAccountAsync(email, password, name, contact, mustChangePassword: true);
    Console.WriteLine(created ? $"Admin '{email}' seeded successfully." : $"Admin '{email}' already exists.");
    return;
}

// 6. Global Exception Middleware (RFC 7807 ProblemDetails - Rule l)
app.UseMiddleware<ExceptionMiddleware>();

// Swagger ONLY in Development (Rule f)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "The Great Ayurveda API v1");
        c.RoutePrefix = "swagger";
    });
    app.MapOpenApi();
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.UseCors("ConfiguredOrigins");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 7. Seed Database & Env Bootstrap (Rules b, e, n)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

    try
    {
        logger.LogInformation("Applying EF Core migrations and initializing data...");
        var optInSeed = app.Configuration.GetValue<bool>("SEED_DATA", false);
        await DbInitializer.SeedAsync(context, app.Environment.IsDevelopment(), optInSeed);

        // Env-var bootstrap admin on first run with forced password change (Rule b)
        var bootstrapEmail = app.Configuration["ADMIN_BOOTSTRAP_EMAIL"];
        var bootstrapPassword = app.Configuration["ADMIN_BOOTSTRAP_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(bootstrapEmail) && !string.IsNullOrWhiteSpace(bootstrapPassword))
        {
            await authService.SeedAdminAccountAsync(bootstrapEmail, bootstrapPassword, "Bootstrap Admin", "9999999999", mustChangePassword: true);
        }

        logger.LogInformation("Database migration and initialization completed successfully.");
    }
    catch (Exception ex)
    {
        // Rule n: Seed failure in Program.cs must stop the app (log + throw) instead of continuing
        logger.LogCritical(ex, "FATAL: Database initialization or migration failed. Aborting application startup: {Message}", ex.Message);
        throw;
    }
}

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
