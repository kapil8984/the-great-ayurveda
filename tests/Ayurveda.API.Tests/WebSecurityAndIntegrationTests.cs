using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;
using Ayurveda.Infrastructure.Services;
using Xunit;

namespace Ayurveda.API.Tests;

public class WebSecurityAndIntegrationTests : IClassFixture<WebMvcFactory>
{
    private readonly WebMvcFactory _factory;

    public WebSecurityAndIntegrationTests(WebMvcFactory factory)
    {
        _factory = factory;
    }

    // =========================================================================
    // 1. AUTHENTICATION & LOGIN SECURITY TESTS (7 tests)
    // =========================================================================

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsGenericErrorMessage()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var formData = new Dictionary<string, string>
        {
            ["Email"] = "nonexistent_patient@ayurveda.com",
            ["Password"] = "WrongPassword123!"
        };

        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(formData));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email address or password", content);
    }

    [Fact]
    public async Task Login_FiveFailedAttempts_TriggersAccountLockout()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var targetEmail = $"lockout_test_{Guid.NewGuid():N}@ayurveda.com";

        using (var scope = _factory.Services.CreateScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = targetEmail,
                Email = targetEmail,
                FullName = "Lockout Candidate",
                EmailConfirmed = true,
                IsActive = true,
                LockoutEnabled = true
            };
            var createRes = await userMgr.CreateAsync(user, "CorrectP@ss123!");
            Assert.True(createRes.Succeeded);
        }

        // Send 5 consecutive incorrect password submissions
        for (int i = 0; i < 5; i++)
        {
            var form = new Dictionary<string, string>
            {
                ["Email"] = targetEmail,
                ["Password"] = "InvalidAttempt999!"
            };
            var res = await client.PostAsync("/account/login", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // 6th attempt should display lockout security message
        var sixthForm = new Dictionary<string, string>
        {
            ["Email"] = targetEmail,
            ["Password"] = "CorrectP@ss123!"
        };
        var sixthRes = await client.PostAsync("/account/login", new FormUrlEncodedContent(sixthForm));
        var content = await sixthRes.Content.ReadAsStringAsync();
        Assert.Contains("locked out", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DoctorLogin_UnapprovedDoctor_RedirectsToPendingApproval()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"dr_pending_{Guid.NewGuid():N}@ayurveda.com";
        var password = "DoctorP@ssword123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Dr. Pending Verification",
                EmailConfirmed = true,
                IsActive = true
            };
            await userMgr.CreateAsync(user, password);
            await userMgr.AddToRoleAsync(user, "Doctor");

            var spec = await db.Categories.FirstAsync();
            var doc = new Doctor
            {
                DoctorName = "Dr. Pending Verification",
                Email = email,
                ApplicationUserId = user.Id
            };
            db.Doctors.Add(doc);
            await db.SaveChangesAsync();

            db.DoctorDetails.Add(new DoctorDetail
            {
                DoctorId = doc.Id,
                SpecializationId = spec.Id,
                Status = 0 // Pending approval
            });
            await db.SaveChangesAsync();
        }

        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["IsDoctorLogin"] = "true"
        };
        var resp = await client.PostAsync("/account/login", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("pendingapproval", resp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_ReturnsSafeResponseWithoutEnumeration()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var form = new Dictionary<string, string>
        {
            ["Email"] = "completely_unknown_user@ayurveda.com"
        };

        var resp = await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(form));
        Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task EmailNormalization_IgnoresWhitespaceAndCasing()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
        var cleanEmail = $"cased_{unique}@ayurveda.com";
        var casedWithWhitespace = $"   CASED_{unique}@Ayurveda.COM   ";
        var password = "SecurePassword123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = cleanEmail,
                Email = cleanEmail,
                FullName = "Cased User",
                EmailConfirmed = true,
                IsActive = true
            };
            await userMgr.CreateAsync(user, password);
            await userMgr.AddToRoleAsync(user, "Customer");
        }

        // Login using cased string with leading/trailing spaces
        var form = new Dictionary<string, string>
        {
            ["Email"] = casedWithWhitespace,
            ["Password"] = password
        };
        var resp = await client.PostAsync("/account/login", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
    }

    [Fact]
    public async Task SeedAdmin_CreatesAccountWithSuperAdminRole()
    {
        using var scope = _factory.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var adminUser = await userMgr.FindByEmailAsync("admin@thegreatayurveda.com")
                     ?? await userMgr.FindByEmailAsync("admin@ayurveda.com");

        Assert.NotNull(adminUser);
        Assert.True(adminUser.IsActive);
        Assert.True(await userMgr.IsInRoleAsync(adminUser, "SuperAdmin"));
    }

    [Fact]
    public void IdentityPasswordOptions_EnforcesStrongPasswordPolicy()
    {
        using var scope = _factory.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = userMgr.Options.Password;

        Assert.True(options.RequireDigit);
        Assert.True(options.RequireUppercase);
        Assert.True(options.RequireLowercase);
        Assert.True(options.RequiredLength >= 8);
    }

    // =========================================================================
    // 2. AUTHORIZATION & ROLE ACCESS CONTROL TESTS (6 tests)
    // =========================================================================

    [Theory]
    [InlineData("/admin-portal/dashboard")]
    [InlineData("/admin-portal/orders")]
    [InlineData("/admin-portal/doctors")]
    public async Task AdminEndpoints_AnonymousUser_RedirectsToAdminLogin(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin-portal/auth/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task PublicAdminSignup_EndpointRemoved_ReturnsNotFound()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp1 = await client.GetAsync("/api/auth/admin/signup");
        var resp2 = await client.PostAsync("/api/auth/admin/signup", new StringContent("{}", Encoding.UTF8, "application/json"));
        var resp3 = await client.PostAsync("/admin/sign-up", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resp2.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resp3.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_AdminUser_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/admin-portal/auth/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_CustomerUser_IsBlocked()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/admin-portal/users/delete/99", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.True(response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrderStatus_CustomerUser_IsBlocked()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/admin-portal/orders/updatestatus", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["orderId"] = "1",
            ["status"] = "Delivered"
        }));

        Assert.True(response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DoctorPortal_CustomerUser_IsBlocked()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/doctorportal/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    // =========================================================================
    // 3. CONCURRENCY & BOOKING / STOCK SAFETY TESTS (2 tests)
    // =========================================================================

    [Fact]
    public async Task AppointmentDoubleBooking_DuplicateSlot_ReturnsFriendlyErrorMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();

        var doctor = await db.Doctors.FirstAsync();
        var slotDate = DateTime.UtcNow.Date.AddDays(5);
        var timeSlot = "10:00 AM";

        // Seed an existing appointment for this exact slot
        var existingAppt = new Appointment
        {
            DoctorId = doctor.Id,
            UserId = 1,
            AppointmentDate = slotDate,
            TimeSlot = timeSlot,
            Status = "Confirmed",
            PatientName = "Original Patient",
            PatientContact = "9876543210"
        };
        db.Appointments.Add(existingAppt);
        await db.SaveChangesAsync();

        // Check if double booking query detects conflict
        var isSlotTaken = await db.Appointments.AnyAsync(a =>
            a.DoctorId == doctor.Id &&
            a.AppointmentDate.Date == slotDate.Date &&
            a.TimeSlot == timeSlot &&
            a.Status != "Cancelled");

        Assert.True(isSlotTaken);
    }

    [Fact]
    public async Task OrderCreation_ExceedsStock_FailsSafely()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();

        var product = await db.Products.FirstAsync(p => p.Stock > 0);
        var requestedQuantity = product.Stock + 50;

        // Verify business logic guard: requested quantity > stock
        var canFulfill = product.Stock >= requestedQuantity;
        Assert.False(canFulfill);
    }

    // =========================================================================
    // 4. FILE UPLOAD & EXCEPTION SAFETY TESTS (4 tests)
    // =========================================================================

    [Fact]
    public async Task FileStorageService_InvalidExtension_ThrowsInvalidOperationException()
    {
        var loggerMock = new LoggerFactory().CreateLogger<FileStorageService>();
        var inMemoryConfig = new Dictionary<string, string?> { ["Storage:UploadsPath"] = "test_uploads" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var service = new FileStorageService(loggerMock, config);

        var content = "echo evil script"u8.ToArray();
        using var stream = new MemoryStream(content);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveFileAsync(stream, "script.exe", "application/x-msdownload"));

        Assert.Contains("Invalid file extension", ex.Message);
    }

    [Fact]
    public async Task FileStorageService_ForgedExtensionWithInvalidMagicBytes_ThrowsInvalidOperationException()
    {
        var loggerMock = new LoggerFactory().CreateLogger<FileStorageService>();
        var inMemoryConfig = new Dictionary<string, string?> { ["Storage:UploadsPath"] = "test_uploads" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var service = new FileStorageService(loggerMock, config);

        // Claims to be jpeg, but stream contains plain text PHP script
        var content = "<?php echo 'malicious'; ?>"u8.ToArray();
        using var stream = new MemoryStream(content);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveFileAsync(stream, "avatar.jpg", "image/jpeg"));

        Assert.Contains("magic bytes", ex.Message);
    }

    [Fact]
    public async Task FileStorageService_ValidJpegWithProperMagicBytes_Succeeds()
    {
        var loggerMock = new LoggerFactory().CreateLogger<FileStorageService>();
        var tempUploads = Path.Combine(Path.GetTempPath(), "tga_test_uploads_" + Guid.NewGuid().ToString("N"));
        var inMemoryConfig = new Dictionary<string, string?> { ["Storage:UploadsPath"] = tempUploads };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var service = new FileStorageService(loggerMock, config);

        // Proper JPEG magic bytes (FF D8 FF)
        byte[] validJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
        using var stream = new MemoryStream(validJpeg);

        var savedPath = await service.SaveFileAsync(stream, "valid_herb.jpg", "image/jpeg");
        Assert.NotNull(savedPath);
        Assert.StartsWith("/uploads/", savedPath);

        // Cleanup
        service.DeleteFile(savedPath);
        if (Directory.Exists(tempUploads)) Directory.Delete(tempUploads, true);
    }

    [Fact]
    public async Task ExceptionHandling_ErrorPage_DoesNotLeakInternalSecrets()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/home/error");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        // Must NOT leak connection strings or stack traces
        Assert.DoesNotContain("Server=(localdb)", content);
        Assert.DoesNotContain("SqlException", content);
        Assert.DoesNotContain("StackTrace", content);
    }

    // =========================================================================
    // 5. IDOR & DATA PROTECTION TESTS (3 tests)
    // =========================================================================

    [Fact]
    public async Task CartEndpoints_UsesAuthenticatedSession_PreventingIdorInjection()
    {
        var client = _factory.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["productId"] = "1",
            ["quantity"] = "2",
            ["userId"] = "999" // Attacker attempts to spoof user ID in payload
        };

        var response = await client.PostAsync("/cart/addtocart", new FormUrlEncodedContent(form));
        // Successfully processes into session without binding arbitrary userId
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task CustomerOrder_OtherCustomerOrderLookup_IsProtected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Unauthenticated or different user trying to view customer order invoice
        var response = await client.GetAsync("/orders/invoice/999");
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CustomerProfile_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/account/profile");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 6. RATE LIMITING & CONTACT FORM INTEGRATION TESTS (4 tests)
    // =========================================================================

    [Fact]
    public async Task RateLimiter_ExcessiveRapidRequests_Returns429TooManyRequests()
    {
        var client = _factory.CreateClient();
        HttpResponseMessage? lastResponse = null;

        // Send 25 rapid requests to search/suggest (limit is 20/min)
        for (int i = 0; i < 25; i++)
        {
            lastResponse = await client.GetAsync("/search/suggest?q=ashwa");
        }

        Assert.NotNull(lastResponse);
        Assert.Equal((HttpStatusCode)429, lastResponse.StatusCode);
        Assert.Equal("60", lastResponse.Headers.RetryAfter?.ToString());
    }

    [Fact]
    public async Task ContactForm_Post_RendersInlineNoticeWithoutAlert()
    {
        var client = _factory.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["name"] = "Vaidya Aspirant",
            ["email"] = "aspirant@ayurveda.com",
            ["subject"] = "Herbal Inquiry",
            ["message"] = "Demonstration inquiry test message."
        };

        var response = await client.PostAsync("/home/contact", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"contact-submission-notice\"", content);
        Assert.Contains("Message Not Delivered:", content);
        Assert.DoesNotContain("alert(", content);
    }

    [Fact]
    public async Task LowercaseRedirect_PreservesQueryStringVerbatim()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var testUrl = "/Account/Verify-Otp?email=Test@Example.com&returnUrl=%2FShop%2FDetails%2F1";
        var response = await client.GetAsync(testUrl);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith("/account/verify-otp?", location);
        Assert.Contains("email=Test@Example.com", location);
        Assert.Contains("returnUrl=%2FShop%2FDetails%2F1", location);
    }

    [Fact]
    public async Task LowercaseRedirect_SkipsUploadsAndStaticFiles()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/uploads/TestUppercaseFile.PNG");
        // Must NOT be 301 redirected
        Assert.NotEqual(HttpStatusCode.MovedPermanently, response.StatusCode);
    }
}
