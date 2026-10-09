using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Ayurveda.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ayurveda.API.Tests;

public class AuthenticationSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthenticationSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsGenericErrorMessage()
    {
        var client = _factory.CreateClient();

        // 1. Non-existent email
        var resp1 = await client.PostAsJsonAsync("/api/auth/user/signin", new
        {
            email = "doesnotexist@ayurveda.com",
            password = "RandomPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp1.StatusCode);
        var content1 = await resp1.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password", content1);

        // 2. Existing user with wrong password
        var signupResp = await client.PostAsJsonAsync("/api/auth/user/signup", new
        {
            username = "TestUser1",
            email = "testuser1@ayurveda.com",
            password = "CorrectPassword123!",
            contact = "9876543211"
        });

        var resp2 = await client.PostAsJsonAsync("/api/auth/user/signin", new
        {
            email = "testuser1@ayurveda.com",
            password = "WrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp2.StatusCode);
        var content2 = await resp2.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password", content2);
    }

    [Fact]
    public async Task Login_FiveFailedAttempts_TriggersAccountLockout()
    {
        var client = _factory.CreateClient();
        var email = "lockout_user@ayurveda.com";

        // Create user
        await client.PostAsJsonAsync("/api/auth/user/signup", new
        {
            username = "LockoutTarget",
            email = email,
            password = "CorrectPassword123!",
            contact = "9876543212"
        });

        // 5 consecutive failed attempts
        for (int i = 0; i < 5; i++)
        {
            var failResp = await client.PostAsJsonAsync("/api/auth/user/signin", new
            {
                email = email,
                password = "WrongPassword"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, failResp.StatusCode);
        }

        // 6th attempt should return lockout message
        var lockedResp = await client.PostAsJsonAsync("/api/auth/user/signin", new
        {
            email = email,
            password = "CorrectPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, lockedResp.StatusCode);
        var content = await lockedResp.Content.ReadAsStringAsync();
        Assert.Contains("locked", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DoctorLogin_UnapprovedDoctor_CannotLogin()
    {
        var client = _factory.CreateClient();
        var email = "unapproved_doc@ayurveda.com";

        // Doctor signs up
        var regResp = await client.PostAsJsonAsync("/api/auth/doctor/signup", new
        {
            doctorName = "Dr. Unapproved",
            email = email,
            password = "DoctorPassword123!",
            contactNumber = "9811223399"
        });
        Assert.Equal(HttpStatusCode.OK, regResp.StatusCode);

        // Attempt login before admin approval
        var loginResp = await client.PostAsJsonAsync("/api/auth/doctor/signin", new
        {
            email = email,
            password = "DoctorPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);
        var content = await loginResp.Content.ReadAsStringAsync();
        Assert.Contains("pending administrator approval", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_ReturnsGenericResponseWithoutLeakingExistence()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email = "absolutelynonexistentuser@ayurveda.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("If the account exists", content);
    }

    [Fact]
    public async Task EmailNormalization_IgnoresWhitespaceAndCasing()
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/user/signup", new
        {
            username = "CasedUser",
            email = "  CASED_USER@Ayurveda.com  ",
            password = "Password123!",
            contact = "9876543213"
        });

        // Sign in with lower case trimmed
        var loginResp = await client.PostAsJsonAsync("/api/auth/user/signin", new
        {
            email = "cased_user@ayurveda.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
    }

    [Fact]
    public async Task SeedAdmin_CreatesAccountWithForcedPasswordChange()
    {
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();

        var email = "cli_admin@ayurveda.com";
        var created = await authService.SeedAdminAccountAsync(email, "StrongAdminPass123!", "CLI Admin", "9876543299", mustChangePassword: true);

        Assert.True(created);
        var admin = db.Admins.FirstOrDefault(a => a.Email == email);
        Assert.NotNull(admin);
        Assert.True(admin.MustChangePassword);
        Assert.Equal("Admin", admin.Role);
    }

    [Fact]
    public void JwtGeneration_KeyTooShort_ThrowsInvalidOperationException()
    {
        using var scope = _factory.Services.CreateScope();
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "short-key-15char", // Less than 32 bytes
            ["Jwt:Issuer"] = "TheGreatAyurveda",
            ["Jwt:Audience"] = "TheGreatAyurvedaApp"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var logger = LoggerFactory.Create(_ => { }).CreateLogger<AuthService>();
        var authService = new AuthService(
            scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>(),
            config,
            scope.ServiceProvider.GetRequiredService<IEmailSender>(),
            logger
        );

        var ex = Assert.Throws<InvalidOperationException>(() => authService.GenerateJwtToken(1, "test@test.com", "Admin", "Admin"));
        Assert.Contains("at least 32 bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
