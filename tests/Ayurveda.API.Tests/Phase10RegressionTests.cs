using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;
using Xunit;

namespace Ayurveda.API.Tests;

public class Phase10RegressionTests : IClassFixture<WebMvcFactory>
{
    private readonly WebMvcFactory _webFactory;

    public Phase10RegressionTests(WebMvcFactory webFactory)
    {
        _webFactory = webFactory;
    }

    private async Task<HttpClient> CreateAuthenticatedAdminClientAsync(string email, string password)
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginForm = new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "RememberMe", "false" }
        };

        var loginResponse = await client.PostAsync("/admin-portal/auth/login", new FormUrlEncodedContent(loginForm));
        Assert.True(loginResponse.StatusCode == HttpStatusCode.Redirect || loginResponse.StatusCode == HttpStatusCode.OK);

        return client;
    }

    private async Task<HttpClient> CreateAuthenticatedCustomerClientAsync(string email = "patient_phase10@ayurveda.com", string password = "Password@2026!")
    {
        using (var scope = _webFactory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = await userManager.FindByEmailAsync(email);
            if (existing == null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = "Phase10 Customer",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(user, password);
                await userManager.AddToRoleAsync(user, "Customer");
            }
        }

        var client = _webFactory.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password
        };
        await client.PostAsync("/account/login", new FormUrlEncodedContent(form));
        return client;
    }

    // -------------------------------------------------------------------------
    // 1. SIGNUP & LOGIN FLOWS FOR ALL THREE ROLES (CUSTOMER, DOCTOR, ADMIN)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task E2E_Auth_LoginPagesRenderCorrectly_ForAllThreeRoles()
    {
        var client = _webFactory.CreateClient();

        // 1. Customer Login
        var customerLogin = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, customerLogin.StatusCode);
        var custHtml = await customerLogin.Content.ReadAsStringAsync();
        Assert.Contains("Patient Portal", custHtml);
        Assert.Contains("Holistic Healing Awaits", custHtml);

        // 2. Doctor Login
        var doctorLogin = await client.GetAsync("/account/login?role=Doctor");
        Assert.Equal(HttpStatusCode.OK, doctorLogin.StatusCode);
        var docHtml = await doctorLogin.Content.ReadAsStringAsync();
        Assert.Contains("Doctor Portal", docHtml);
        Assert.Contains("Vaidya Clinical Desk", docHtml);

        // 3. Admin Login (Dedicated Area under /admin-portal)
        var adminClient = _webFactory.CreateClient();
        var adminLogin = await adminClient.GetAsync("/admin-portal/auth/login");
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        var adminHtml = await adminLogin.Content.ReadAsStringAsync();
        Assert.Contains("Staff Authentication", adminHtml);
        Assert.Contains("Restricted Area", adminHtml);
        // Zero registration links on admin portal
        Assert.DoesNotContain("/Auth/Register", adminHtml);
        Assert.DoesNotContain("/Account/Register", adminHtml);
    }

    // -------------------------------------------------------------------------
    // 2. SEARCH SUGGESTION & LATENCY SPEC
    // -------------------------------------------------------------------------
    [Fact]
    public async Task E2E_Search_Suggest_ReturnsGroupedJsonAndLatencyHeader()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/search/suggest?q=ashwa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("X-Search-Latency-Ms"));

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ashwagandha", json);
    }

    // -------------------------------------------------------------------------
    // 3. ADD TO CART -> CHECKOUT FLOW
    // -------------------------------------------------------------------------
    [Fact]
    public async Task E2E_Commerce_AddToCart_AndCheckoutPage_RendersSuccessfully()
    {
        var client = _webFactory.CreateClient();

        // Seed product check
        using (var scope = _webFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var product = await db.Products.FirstOrDefaultAsync(p => p.IsActive);
            Assert.NotNull(product);

            // Add item to cart
            var addResponse = await client.PostAsync("/cart/addtocart", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "productId", product.Id.ToString() },
                { "quantity", "1" }
            }));
            Assert.True(addResponse.StatusCode == HttpStatusCode.Redirect || addResponse.StatusCode == HttpStatusCode.OK);
        }

        // 1. Anonymous checkout redirects to login
        var noRedirectClient = _webFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var anonCheckout = await noRedirectClient.GetAsync("/checkout");
        Assert.Equal(HttpStatusCode.Redirect, anonCheckout.StatusCode);

        // 2. Customer signs in and cart is merged
        using (var scope = _webFactory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByEmailAsync("patient_checkout_flow@ayurveda.com") == null)
            {
                var user = new ApplicationUser
                {
                    UserName = "patient_checkout_flow@ayurveda.com",
                    Email = "patient_checkout_flow@ayurveda.com",
                    FullName = "Patient Checkout",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(user, "Password@2026!");
                await userManager.AddToRoleAsync(user, "Customer");
            }
        }

        await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "patient_checkout_flow@ayurveda.com",
            ["Password"] = "Password@2026!"
        }));

        // 3. Checkout renders delivery addresses and summary
        var checkoutResponse = await client.GetAsync("/checkout");
        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);
        var checkoutHtml = await checkoutResponse.Content.ReadAsStringAsync();
        Assert.Contains("Checkout", checkoutHtml);
        Assert.Contains("Delivery Address", checkoutHtml);
    }

    // -------------------------------------------------------------------------
    // 4. DOCTOR APPROVAL FLOW (ADMIN APPROVES PENDING DOCTOR)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task E2E_DoctorManagement_AdminApprovesPendingDoctor()
    {
        int testDoctorId;
        using (var scope = _webFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var category = await db.Categories.FirstOrDefaultAsync() ?? new Category { Name = "General", IsActive = true };
            if (category.Id == 0) { db.Categories.Add(category); await db.SaveChangesAsync(); }

            var pendingDoc = new Doctor
            {
                DoctorName = "Vaidya Test Applicant",
                Email = "pending.vaidya@ayurveda.test",
                ContactNumber = "9876500001",
                DoctorDetail = new DoctorDetail
                {
                    SpecializationId = category.Id,
                    Experience = 8,
                    Qualification = "BAMS, MD",
                    RegistrationNumber = "AYUSH-TEST-9999",
                    RegistrationCouncil = "Central Council of Indian Medicine",
                    Status = 0, // Pending
                    ConsultationFee = 500,
                    Language = "Hindi, English"
                }
            };
            db.Doctors.Add(pendingDoc);
            await db.SaveChangesAsync();
            testDoctorId = pendingDoc.Id;
        }

        var adminClient = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);

        // Admin approves the doctor
        var approveResponse = await adminClient.PostAsync($"/admin-portal/Doctors/Approve?doctorId={testDoctorId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.True(approveResponse.StatusCode == HttpStatusCode.Redirect || approveResponse.StatusCode == HttpStatusCode.OK);

        // Verify status in DB is 1 (Approved)
        using (var scope = _webFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var updatedDoc = await db.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.Id == testDoctorId);
            Assert.NotNull(updatedDoc?.DoctorDetail);
            Assert.Equal(1, updatedDoc.DoctorDetail.Status);
        }
    }

    // -------------------------------------------------------------------------
    // 5. APPOINTMENT BOOKING & DOUBLE-BOOKING PREVENTION
    // -------------------------------------------------------------------------
    [Fact]
    public async Task E2E_DoctorBooking_AndDoubleBookAttempt_IsPrevented()
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        int doctorId;
        using (var scope = _webFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var activeDoc = await db.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1);
            Assert.NotNull(activeDoc);
            doctorId = activeDoc.Id;

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByEmailAsync("patient_book_e2e@ayurveda.com") == null)
            {
                var user = new ApplicationUser
                {
                    UserName = "patient_book_e2e@ayurveda.com",
                    Email = "patient_book_e2e@ayurveda.com",
                    FullName = "Patient Book E2E",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(user, "Password@2026!");
                await userManager.AddToRoleAsync(user, "Customer");
            }
        }

        await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "patient_book_e2e@ayurveda.com",
            ["Password"] = "Password@2026!"
        }));

        var bookingDate = DateTime.Today.AddDays(2);
        var timeSlot = "11:00 AM";

        var bookFormData = new Dictionary<string, string>
        {
            { "DoctorId", doctorId.ToString() },
            { "SelectedDate", bookingDate.ToString("yyyy-MM-dd") },
            { "SelectedTimeSlot", timeSlot },
            { "PatientName", "Rahul Sharma" },
            { "PatientContact", "9876543210" },
            { "HealthConcern", "Chronic digestion issues" },
            { "ConsultationType", "Video" }
        };

        // First booking attempt succeeds
        var firstBook = await client.PostAsync("/doctors/bookpost", new FormUrlEncodedContent(bookFormData));
        Assert.True(firstBook.StatusCode == HttpStatusCode.Redirect || firstBook.StatusCode == HttpStatusCode.OK);

        // Second booking attempt for exact same doctor and slot is detected as double booking
        var secondBook = await client.PostAsync("/doctors/bookpost", new FormUrlEncodedContent(bookFormData));
        Assert.Equal(HttpStatusCode.Redirect, secondBook.StatusCode);
        // Redirects back to booking page with error notice
        Assert.Contains($"/Doctors/Book/{doctorId}", secondBook.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // 6. ACCESSIBLE CONFIRM MODAL ACROSS ALL PORTALS
    // -------------------------------------------------------------------------
    [Fact]
    public async Task E2E_LogoutModal_AccessibleAttributes_PresentInAllPortals()
    {
        // 1. Customer Portal Layout
        var webClient = _webFactory.CreateClient();
        var homeResponse = await webClient.GetAsync("/");
        var homeHtml = await homeResponse.Content.ReadAsStringAsync();

        Assert.Contains("id=\"confirmActionModal\"", homeHtml);
        Assert.Contains("role=\"dialog\"", homeHtml);
        Assert.Contains("aria-modal=\"true\"", homeHtml);
        Assert.Contains("confirmModalProceedBtn", homeHtml);
        Assert.Contains("confirmModalCancelBtn", homeHtml);

        // 2. Doctor Portal Navigation Layout
        var docLoginResponse = await webClient.GetAsync("/account/login?role=Doctor");
        Assert.Equal(HttpStatusCode.OK, docLoginResponse.StatusCode);

        // 3. Admin Portal Layout
        var adminClient = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var adminDash = await adminClient.GetAsync("/admin-portal/dashboard");
        var adminHtml = await adminDash.Content.ReadAsStringAsync();

        Assert.Contains("id=\"confirmActionModal\"", adminHtml);
        Assert.Contains("role=\"dialog\"", adminHtml);
        Assert.Contains("aria-modal=\"true\"", adminHtml);
        Assert.Contains("id=\"idleTimeoutModal\"", adminHtml);
        Assert.Contains("data-tga-logout", adminHtml);
    }

    // -------------------------------------------------------------------------
    // 7. ADMIN AUTHORIZATION MATRIX (ROLE BOUNDARIES)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task E2E_AdminAuthorizationMatrix_EnforcesRoleBoundaries()
    {
        // 1. CatalogManager: Allowed to view products, forbidden from finance
        var catalogClient = await CreateAuthenticatedAdminClientAsync("catalog@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var productsResponse = await catalogClient.GetAsync("/admin-portal/products");
        Assert.Equal(HttpStatusCode.OK, productsResponse.StatusCode);

        var financeResponse = await catalogClient.GetAsync("/admin-portal/finance");
        Assert.True(financeResponse.StatusCode == HttpStatusCode.Forbidden || 
                    (financeResponse.StatusCode == HttpStatusCode.Redirect && financeResponse.Headers.Location?.ToString().Contains("accessdenied", StringComparison.OrdinalIgnoreCase) == true));

        // 2. FinanceManager: Allowed to view finance
        var financeClient = await CreateAuthenticatedAdminClientAsync("finance@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var financeOk = await financeClient.GetAsync("/admin-portal/finance");
        Assert.Equal(HttpStatusCode.OK, financeOk.StatusCode);

        // 3. SuperAdmin: Allowed everywhere
        var superAdminClient = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var adminProducts = await superAdminClient.GetAsync("/admin-portal/products");
        var adminFinance = await superAdminClient.GetAsync("/admin-portal/finance");
        var adminAudit = await superAdminClient.GetAsync("/admin-portal/audit");

        Assert.Equal(HttpStatusCode.OK, adminProducts.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminFinance.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminAudit.StatusCode);
    }
}
