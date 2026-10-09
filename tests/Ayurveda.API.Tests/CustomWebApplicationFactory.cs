using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ayurveda.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Ayurveda.API.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Ayurveda.API.Program>
{
    public const string TestJwtKey = "DevelopmentSecureSecretKey2026_AtLeast32BytesLongForHmacSha256!";
    private readonly string _dbName = "TestAyurvedaDb_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var inMemoryConfig = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = "TheGreatAyurveda",
                ["Jwt:Audience"] = "TheGreatAyurvedaApp",
                ["Jwt:ExpiresInMinutes"] = "30"
            };
            config.AddInMemoryCollection(inMemoryConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing database provider registrations
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AyurvedaDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AyurvedaDbContext) ||
                d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true ||
                d.ServiceType.Name.Contains("DbContextOptionsConfiguration")).ToList();

            foreach (var d in descriptorsToRemove)
            {
                services.Remove(d);
            }

            // Add InMemory Database
            services.AddDbContext<AyurvedaDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }

    public HttpClient CreateAuthenticatedClient(string role, int id = 1, string email = "test@ayurveda.com", string name = "Test User")
    {
        var client = CreateClient();
        var token = GenerateTestToken(id, email, role, name);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static string GenerateTestToken(int id, string email, string role, string name)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role),
            new Claim(ClaimTypes.Name, name),
            new Claim("sub", id.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "TheGreatAyurveda",
            audience: "TheGreatAyurvedaApp",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
