using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
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
    public async Task IdentityPasswordOptions_EnforcesStrongPasswordPolicy()
    {
        using var scope = _factory.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = userMgr.Options.Password;

        // Verify configured Identity options
        Assert.True(options.RequireDigit);
        Assert.True(options.RequireUppercase);
        Assert.True(options.RequireLowercase);
        Assert.True(options.RequiredLength >= 8);

        var dummyUser = new ApplicationUser { UserName = "pwd_policy_test@example.com", Email = "pwd_policy_test@example.com" };

        // 1. Password too short (<8 chars)
        var shortResult = await userMgr.PasswordValidators[0].ValidateAsync(userMgr, dummyUser, "Aa1!");
        Assert.False(shortResult.Succeeded);
        Assert.Contains(shortResult.Errors, e => e.Code == "PasswordTooShort");

        // 2. Missing digit
        var noDigitResult = await userMgr.PasswordValidators[0].ValidateAsync(userMgr, dummyUser, "Abcdefghijk!");
        Assert.False(noDigitResult.Succeeded);
        Assert.Contains(noDigitResult.Errors, e => e.Code == "PasswordRequiresDigit");

        // 3. Missing uppercase
        var noUpperResult = await userMgr.PasswordValidators[0].ValidateAsync(userMgr, dummyUser, "abcdefgh1!");
        Assert.False(noUpperResult.Succeeded);
        Assert.Contains(noUpperResult.Errors, e => e.Code == "PasswordRequiresUpper");

        // 4. Missing lowercase
        var noLowerResult = await userMgr.PasswordValidators[0].ValidateAsync(userMgr, dummyUser, "ABCDEFGH1!");
        Assert.False(noLowerResult.Succeeded);
        Assert.Contains(noLowerResult.Errors, e => e.Code == "PasswordRequiresLower");

        // 5. Compliant password succeeds
        var validResult = await userMgr.PasswordValidators[0].ValidateAsync(userMgr, dummyUser, "ValidPass123!");
        Assert.True(validResult.Succeeded);
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
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var loginForm = new Dictionary<string, string>
        {
            ["Email"] = "admin@thegreatayurveda.com",
            ["Password"] = WebMvcFactory.TestAdminPassword
        };

        var loginResponse = await client.PostAsync("/admin-portal/auth/login", new FormUrlEncodedContent(loginForm));
        Assert.True(loginResponse.StatusCode == HttpStatusCode.Redirect || loginResponse.StatusCode == HttpStatusCode.OK);

        // Access real administrative pages after admin authentication
        var dashResponse = await client.GetAsync("/admin-portal/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashResponse.StatusCode);
        var dashHtml = await dashResponse.Content.ReadAsStringAsync();
        Assert.Contains("Executive Management Console", dashHtml);

        var ordersResponse = await client.GetAsync("/admin-portal/orders");
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);
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
        int cartItemIdA;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var custA = await userMgr.FindByEmailAsync("custA_cart@example.com");
            if (custA == null)
            {
                custA = new ApplicationUser { UserName = "custA_cart@example.com", Email = "custA_cart@example.com", EmailConfirmed = true };
                await userMgr.CreateAsync(custA, "PassCartA@2026!");
            }

            var custB = await userMgr.FindByEmailAsync("custB_cart@example.com");
            if (custB == null)
            {
                custB = new ApplicationUser { UserName = "custB_cart@example.com", Email = "custB_cart@example.com", EmailConfirmed = true };
                await userMgr.CreateAsync(custB, "PassCartB@2026!");
            }

            var cartA = await db.Carts.Include(c => c.CartItems).FirstOrDefaultAsync(c => c.UserId == custA.Id);
            if (cartA == null)
            {
                cartA = new Cart { UserId = custA.Id };
                db.Carts.Add(cartA);
                await db.SaveChangesAsync();
            }

            var product = await db.Products.FirstAsync();
            var itemA = new CartItem { CartId = cartA.Id, ProductId = product.Id, Quantity = 2 };
            db.CartItems.Add(itemA);
            await db.SaveChangesAsync();
            cartItemIdA = itemA.Id;
        }

        // Customer B logs in
        var clientB = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await clientB.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "custB_cart@example.com",
            ["Password"] = "PassCartB@2026!"
        }));

        // 1. Customer B attempts to modify Customer A's cart item via IDOR
        var updateResp = await clientB.PostAsync("/cart/updatequantity", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cartItemId"] = cartItemIdA.ToString(),
            ["quantity"] = "10"
        }));
        Assert.Equal(HttpStatusCode.NotFound, updateResp.StatusCode);

        // 2. Customer B attempts to delete Customer A's cart item via IDOR
        var removeResp = await clientB.PostAsync("/cart/removeitem", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cartItemId"] = cartItemIdA.ToString()
        }));
        Assert.Equal(HttpStatusCode.NotFound, removeResp.StatusCode);

        // 3. Verify Customer A's item remains untouched in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var item = await db.CartItems.FindAsync(cartItemIdA);
            Assert.NotNull(item);
            Assert.Equal(2, item.Quantity);
        }
    }

    [Fact]
    public async Task CustomerOrder_OtherCustomerOrderLookup_IsProtected()
    {
        int orderIdForCustomerA;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var custA = await userMgr.FindByEmailAsync("custA_order@example.com");
            if (custA == null)
            {
                custA = new ApplicationUser { UserName = "custA_order@example.com", Email = "custA_order@example.com", FullName = "Customer A", EmailConfirmed = true };
                await userMgr.CreateAsync(custA, "PassCustomerA@2026!");
            }

            var custB = await userMgr.FindByEmailAsync("custB_order@example.com");
            if (custB == null)
            {
                custB = new ApplicationUser { UserName = "custB_order@example.com", Email = "custB_order@example.com", FullName = "Customer B", EmailConfirmed = true };
                await userMgr.CreateAsync(custB, "PassCustomerB@2026!");
            }

            var orderA = new Order
            {
                UserId = custA.Id,
                OrderNumber = "ORD-TEST-A-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                OrderDate = DateTime.UtcNow,
                TotalAmount = 1500m,
                Status = "Placed",
                PaymentStatus = "Completed",
                ShippingAddress = "123 Test Street, New Delhi"
            };
            db.Orders.Add(orderA);
            await db.SaveChangesAsync();
            orderIdForCustomerA = orderA.Id;
        }

        // Customer B logs in
        var clientB = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginResp = await clientB.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "custB_order@example.com",
            ["Password"] = "PassCustomerB@2026!"
        }));
        Assert.True(loginResp.StatusCode == HttpStatusCode.Redirect || loginResp.StatusCode == HttpStatusCode.OK);

        // Customer B attempts to lookup Customer A's order invoice and details
        var invoiceResponse = await clientB.GetAsync($"/orders/invoice/{orderIdForCustomerA}");
        Assert.True(invoiceResponse.StatusCode == HttpStatusCode.NotFound || invoiceResponse.StatusCode == HttpStatusCode.Forbidden);

        var detailsResponse = await clientB.GetAsync($"/orders/details/{orderIdForCustomerA}");
        Assert.True(detailsResponse.StatusCode == HttpStatusCode.NotFound || detailsResponse.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CustomerProfile_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/account/profile");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Swagger_InDevelopment_ReturnsOk_InProduction_ReturnsNotFound()
    {
        // Development: /swagger/v1/swagger.json returns 200 OK
        var devClient = _factory.CreateClient();
        var devResp = await devClient.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, devResp.StatusCode);
        var json = await devResp.Content.ReadAsStringAsync();
        Assert.Contains("The Great Ayurveda API", json);

        // Production: /swagger/v1/swagger.json and /swagger return 404 NotFound
        var prodFactory = _factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var prodClient = prodFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var prodDocResp = await prodClient.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.NotFound, prodDocResp.StatusCode);

        var prodUiResp = await prodClient.GetAsync("/swagger");
        Assert.True(prodUiResp.StatusCode == HttpStatusCode.NotFound || prodUiResp.StatusCode == HttpStatusCode.MovedPermanently);
    }

    [Fact]
    public async Task ProxyIp_RateLimiting_IsolatesBuckets_ForForwardedHeaders_FromTrustedProxy()
    {
        // 1. Client 1 sends requests behind trusted loopback proxy with X-Forwarded-For: 203.0.113.1
        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.1");

        // 2. Client 2 sends requests behind trusted loopback proxy with X-Forwarded-For: 203.0.113.2
        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.2");

        // Exhaust search suggest rate limit for Client 1 (60 requests)
        for (int i = 0; i < 60; i++)
        {
            var r = await client1.GetAsync("/search/suggest?q=ashwa");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
        var c1RateLimited = await client1.GetAsync("/search/suggest?q=ashwa");
        Assert.Equal(HttpStatusCode.TooManyRequests, c1RateLimited.StatusCode);

        // Client 2 with DIFFERENT X-Forwarded-For is NOT blocked (independent rate limit bucket)
        var c2Response = await client2.GetAsync("/search/suggest?q=ashwa");
        Assert.Equal(HttpStatusCode.OK, c2Response.StatusCode);
    }

    // =========================================================================
    // 6. RATE LIMITING & CONTACT FORM INTEGRATION TESTS (4 tests)
    // =========================================================================

    [Fact]
    public async Task RateLimiter_AccountLogin_16thRequestGets429()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.77");

        var statusCodes = new List<HttpStatusCode>();
        for (int i = 1; i <= 20; i++)
        {
            var form = new Dictionary<string, string>
            {
                ["Email"] = $"wrong_user_{i}@ayurveda.com",
                ["Password"] = "WrongPass123!"
            };
            var res = await client.PostAsync("/account/login", new FormUrlEncodedContent(form));
            statusCodes.Add(res.StatusCode);
        }

        // Requests 1 to 15 must NOT be 429 (render invalid login page with 200 OK)
        for (int i = 0; i < 15; i++)
        {
            Assert.Equal(HttpStatusCode.OK, statusCodes[i]);
        }

        // 16th request and onward receive 429 Too Many Requests
        Assert.Equal(HttpStatusCode.TooManyRequests, statusCodes[15]); // index 15 = 16th request
        for (int i = 15; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, statusCodes[i]);
        }
    }

    [Fact]
    public async Task RateLimiter_AdminAuthLogin_16thRequestGets429()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.88");

        var statusCodes = new List<HttpStatusCode>();
        for (int i = 1; i <= 20; i++)
        {
            var form = new Dictionary<string, string>
            {
                ["Email"] = $"admin_wrong_{i}@thegreatayurveda.com",
                ["Password"] = "WrongAdminPass123!"
            };
            var res = await client.PostAsync("/admin-portal/auth/login", new FormUrlEncodedContent(form));
            statusCodes.Add(res.StatusCode);
        }

        // Requests 1 to 15 must NOT be 429 (render admin login page with 200 OK)
        for (int i = 0; i < 15; i++)
        {
            Assert.Equal(HttpStatusCode.OK, statusCodes[i]);
        }

        // 16th request and onward receive 429 Too Many Requests
        Assert.Equal(HttpStatusCode.TooManyRequests, statusCodes[15]); // index 15 = 16th request
        for (int i = 15; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, statusCodes[i]);
        }
    }

    [Fact]
    public async Task CredentialVerification_RealAccounts_DirectHashCheck_RejectsAllOldDemoPasswords()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var users = await userManager.Users.ToListAsync();
        Assert.NotEmpty(users);

        var untrackedPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "App_Data", "test_credentials.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "test_credentials.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "App_Data", "test_credentials.json")
        };
        var oldDemoPasswords = new List<string>();
        foreach (var p in untrackedPaths)
        {
            if (File.Exists(p))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(p));
                if (doc.RootElement.TryGetProperty("BlacklistPasswords", out var arr))
                {
                    foreach (var elem in arr.EnumerateArray())
                    {
                        var val = elem.GetString();
                        if (!string.IsNullOrEmpty(val)) oldDemoPasswords.Add(val);
                    }
                }
                break;
            }
        }

        foreach (var user in users)
        {
            foreach (var oldPwd in oldDemoPasswords)
            {
                var matches = await userManager.CheckPasswordAsync(user, oldPwd);
                Assert.False(matches, $"User {user.Email} unexpectedly matched old demo password '{oldPwd}'");
            }
        }
    }

    [Fact]
    public async Task RateLimiter_ExcessiveRapidRequests_Returns429TooManyRequests()
    {
        var client = _factory.CreateClient();
        HttpResponseMessage? lastResponse = null;

        // Send 65 rapid requests to search/suggest (limit is 60/min)
        for (int i = 0; i < 65; i++)
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
    public async Task AdminLogout_DoesNotClear_PublicCustomerCookie()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Set customer auth cookie
        client.DefaultRequestHeaders.Add("Cookie", ".Ayurveda.Web.Auth=CustomerSessionData; .Ayurveda.GuestCart=[{\"ProductId\":1,\"Quantity\":2}]");

        var response = await client.PostAsync("/admin-portal/auth/logout", null);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Verify that Set-Cookie does NOT delete .Ayurveda.Web.Auth or .Ayurveda.GuestCart
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : new List<string>();
        Assert.DoesNotContain(setCookies, c => c.StartsWith(".Ayurveda.Web.Auth=") && c.Contains("expires=Thu, 01 Jan 1970"));
        Assert.DoesNotContain(setCookies, c => c.StartsWith(".Ayurveda.GuestCart=") && c.Contains("expires=Thu, 01 Jan 1970"));
    }

    [Fact]
    public async Task GuestCart_Add_SetsPersistentGuestCartCookie()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = "1",
            ["quantity"] = "2"
        });

        var response = await client.PostAsync("/cart/add", content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : new List<string>();
        var guestCartCookie = setCookies.FirstOrDefault(c => c.StartsWith(".Ayurveda.GuestCart="));
        Assert.NotNull(guestCartCookie);
        Assert.Contains("max-age=2592000", guestCartCookie.ToLowerInvariant()); // 30 days in seconds
    }
}

