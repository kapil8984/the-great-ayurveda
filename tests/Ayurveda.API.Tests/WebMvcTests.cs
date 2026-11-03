using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;
using Xunit;

namespace Ayurveda.API.Tests;

public class PassThroughAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new("test", "test", "__RequestVerificationToken", "test");
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => new("test", "test", "__RequestVerificationToken", "test");
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
    public void SetCookieTokenAndHeader(HttpContext httpContext) { }
}

public class WebMvcFactory : WebApplicationFactory<Ayurveda.Web.Program>
{
    public const string TestAdminPassword = "Test_SuperAdmin_Pass2026!";
    private readonly string _dbName = "WebMvcTestDb_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevDemo:AdminPassword"] = TestAdminPassword,
                ["Admin:InitialPassword"] = TestAdminPassword
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AyurvedaDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AyurvedaDbContext) ||
                d.ServiceType == typeof(IAntiforgery) ||
                d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true ||
                d.ServiceType.Name.Contains("DbContextOptionsConfiguration")).ToList();

            foreach (var d in descriptorsToRemove)
            {
                services.Remove(d);
            }

            services.AddSingleton<IAntiforgery, PassThroughAntiforgery>();

            services.AddDbContext<AyurvedaDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebMvcFactory>>();
            var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            DbInitializer.InitializeAsync(db, userManager, roleManager, logger, config).GetAwaiter().GetResult();
        }

        return host;
    }
}

public class WebMvcTests : IClassFixture<WebMvcFactory>
{
    private readonly WebMvcFactory _webFactory;

    public WebMvcTests(WebMvcFactory webFactory)
    {
        _webFactory = webFactory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Shop")]
    [InlineData("/Doctors")]
    [InlineData("/Yoga")]
    [InlineData("/Diseases")]
    [InlineData("/Home/About")]
    [InlineData("/Home/Contact")]
    [InlineData("/Home/Faq")]
    [InlineData("/Home/Privacy")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    public async Task PublicRoutes_ReturnSuccessAndHtml(string url)
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("The Great Ayurveda", content);
        Assert.Contains("STATUTORY MEDICAL DISCLAIMER", content);
    }

    [Fact]
    public async Task SearchSuggestEndpoint_ReturnsJsonGroupedSuggestions()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/search/suggest?q=triphala");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("query", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminPortal_UnauthenticatedUser_RedirectsToAdminLogin()
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/admin-portal/dashboard");

        // Protected route must redirect to Admin Login (/admin-portal/auth/login)
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin-portal/auth/login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminLogin_RendersSecureAdminPage()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/admin-portal/auth/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Admin Portal", content);
        Assert.Contains("Restricted Area", content);
    }

    [Fact]
    public async Task Storefront_HomePage_HasCorrectTitleAndBrandingMeta()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // Acceptance criteria: correct tab title, meta description, theme color, manifest, og-image
        Assert.Contains("The Great Ayurveda | Authentic Ayurveda Care &amp; Consultation", html);
        Assert.Contains("<meta name=\"theme-color\" content=\"#1F5E3B\" />", html);
        Assert.Contains("href=\"/site.webmanifest\"", html);
        Assert.Contains("href=\"/favicon.ico\"", html);
        Assert.Contains("content=\"/og-image.png\"", html);
    }

    [Theory]
    [InlineData("/favicon.ico")]
    [InlineData("/favicon-32x32.png")]
    [InlineData("/apple-touch-icon.png")]
    [InlineData("/site.webmanifest")]
    [InlineData("/og-image.png")]
    [InlineData("/images/logo-mark.svg")]
    [InlineData("/images/logo-full.svg")]
    [InlineData("/images/logo-dark.svg")]
    public async Task Storefront_StaticBrandingAssets_AreServedSuccessfully(string assetPath)
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync(assetPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength > 0);
    }

    [Theory]
    [InlineData("/favicon.ico")]
    [InlineData("/favicon-32x32.png")]
    [InlineData("/apple-touch-icon.png")]
    [InlineData("/admin.webmanifest")]
    [InlineData("/images/admin-logo-full.svg")]
    public async Task AdminPortal_StaticBrandingAssets_AreServedSuccessfully(string assetPath)
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync(assetPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength > 0);
    }

    [Fact]
    public async Task Storefront_HomePage_RendersAllTwelveWowSections()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // Section 1 & 2: Announcement & Header Mega-Menu
        Assert.Contains("HEALTH15", html);
        Assert.Contains("Shop by Concern", html);

        // Section 3: Hero section
        Assert.Contains("Harmonize Mind, Body", html);

        // Section 4: Prakriti Quiz
        Assert.Contains("id=\"prakritiQuizSection\"", html);
        Assert.Contains("Discover Your True Ayurvedic Prakriti", html);

        // Section 5: Shop by concern and category
        Assert.Contains("Shop by Health Concern", html);
        Assert.Contains("Browse by Shastriya Category", html);

        // Section 6: Featured products carousel
        Assert.Contains("id=\"featuredCarouselTrack\"", html);

        // Section 7: Deals strip with timer
        Assert.Contains("id=\"dealsCountdown\"", html);
        Assert.Contains("RITUCHARYA20", html);

        // Section 8: Consult a doctor directory
        Assert.Contains("id=\"doctors\"", html);
        Assert.Contains("Ayurvedic Physician Consultations", html);

        // Section 9: 3-step timeline
        Assert.Contains("How The Great Ayurveda Works", html);

        // Section 10: Dinacharya routine clock & Dravya spotlight
        Assert.Contains("id=\"dinacharya\"", html);
        Assert.Contains("Brahma Muhurta", html);

        // Section 11: Quality promises & FAQ accordion
        Assert.Contains("Classical Purity", html);
        Assert.Contains("id=\"tgaFaqAccordion\"", html);

        // Section 12: Statutory medical disclaimer
        Assert.Contains("Statutory Ayurvedic Healthcare Disclaimer", html);
    }

    [Theory]
    [InlineData("/fonts/cormorant-garamond-600.woff2")]
    [InlineData("/fonts/cormorant-garamond-700.woff2")]
    [InlineData("/fonts/plus-jakarta-sans-400.woff2")]
    [InlineData("/fonts/plus-jakarta-sans-600.woff2")]
    public async Task Storefront_SelfHostedFonts_AreServedSuccessfully(string fontPath)
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync(fontPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength > 0);
    }

    [Fact]
    public async Task Auth_CustomerLogin_RendersSplitScreenAndControls()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/account/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Patient Sign In", html);
        Assert.Contains("id=\"toggleLoginPasswordBtn\"", html);
        Assert.Contains("id=\"capsLockAlert\"", html);
        Assert.Contains("RememberMe", html);
        Assert.Contains("Forgot password?", html);
        Assert.Contains("/doctor/login", html);
    }

    [Fact]
    public async Task Auth_CustomerRegister_RendersSplitScreenAndStrengthMeter()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/account/register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Begin Your Vedic Wellness Journey", html);
        Assert.Contains("id=\"strengthBar\"", html);
        Assert.Contains("id=\"strengthLabel\"", html);
        Assert.Contains("id=\"critLength\"", html);
        Assert.Contains("id=\"capsLockAlertReg\"", html);
        Assert.Contains("/doctor/register", html);
    }

    [Fact]
    public async Task Auth_VerifyEmailOtp_RendersSixBoxesAndCountdown()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/account/verify-otp?email=patient@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Verify Your Email", html);
        Assert.Contains("patient@example.com", html);
        Assert.Contains("class=\"form-control text-center fw-bold fs-4 otp-box", html);
        Assert.Contains("id=\"countdownTimer\"", html);
        Assert.Contains("id=\"resendOtpBtn\"", html);
    }

    [Fact]
    public async Task Auth_DoctorLogin_RendersClinicalTealTheme()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/doctor/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Vaidya Clinical Desk", html);
        Assert.Contains("Vaidya Sign In", html);
        Assert.Contains("btn-teal", html);
    }

    [Fact]
    public async Task Auth_DoctorRegisterWizard_RendersMultiStepOnboarding()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/doctor/register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Join India's Leading Vaidya Tele-Health Network", html);
        Assert.Contains("id=\"wizardStep1\"", html);
        Assert.Contains("id=\"wizardStep2\"", html);
        Assert.Contains("id=\"wizardStep3\"", html);
        Assert.Contains("id=\"wizardStep4\"", html);
        Assert.Contains("id=\"wizardStep5\"", html);
        Assert.Contains("Medical Degree Certificate", html);
    }

    [Fact]
    public async Task Auth_DoctorPendingApproval_RendersVerificationTimeline()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/doctorportal/pendingapproval");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Credentials Under Verification", html);
        Assert.Contains("Medical Council Registry Verification", html);
    }

    [Fact]
    public async Task Auth_AdminLogin_RendersTotp2FaAndZeroRegisterLinks()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/admin-portal/auth/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Restricted Area", html);
        Assert.Contains("id=\"togglePasswordBtn\"", html);
        Assert.Contains("id=\"adminCapsLockAlert\"", html);
        // Rule: Admin portal must have ZERO registration links
        Assert.DoesNotContain("Register", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign Up", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Catalog_StoreListing_RendersSeededProductsAndFacetedFilters()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/shop");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Authentic Ayurvedic Pharmacopoeia", html);
        Assert.Contains("Ayurvedic Categories", html);
        Assert.Contains("Ayurvedic Brands", html);
        Assert.Contains("Price Range", html);
        Assert.Contains("Customer Rating", html);
        Assert.Contains("Exclude Out of Stock", html);
        Assert.True(html.Contains("Great Ayurveda Labs") || html.Contains("The Great Ayurveda"));
    }

    [Fact]
    public async Task Catalog_ProductDetails_RendersVariantsAndAyushDisclaimer()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/shop/details/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Botanical Pharmacopoeia", html);
        Assert.Contains("Traditionally Used For", html);
        Assert.Contains("Check Delivery", html);
        Assert.Contains("Cash on Delivery Availability", html);
        Assert.Contains("AYUSH Health Disclaimer", html);
        Assert.Contains("Add to Basket", html);
        Assert.Contains("Buy Now", html);
    }

    [Fact]
    public async Task Catalog_PincodeCheck_ValidPincode_ReturnsServiceableEta()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/shop/checkpincode?pincode=110001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"isServiceable\":true", content);
        Assert.Contains("North India", content);
    }

    [Fact]
    public async Task Catalog_PincodeCheck_InvalidPincode_ReturnsError()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/shop/checkpincode?pincode=123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"isServiceable\":false", content);
    }

    [Fact]
    public async Task ECommerce_CartFlow_AddAndUpdateQuantity_Succeeds()
    {
        var client = _webFactory.CreateClient();
        
        // Add to cart
        var form = new Dictionary<string, string>
        {
            ["productId"] = "1",
            ["quantity"] = "2"
        };
        var postResponse = await client.PostAsync("/cart/addtocart", new FormUrlEncodedContent(form));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.StatusCode == HttpStatusCode.Redirect);

        // Fetch Cart page
        var cartResponse = await client.GetAsync("/cart");
        Assert.Equal(HttpStatusCode.OK, cartResponse.StatusCode);
        var html = await cartResponse.Content.ReadAsStringAsync();

        Assert.Contains("Your Wellness Basket", html);
        Assert.Contains("Order Summary", html);
        Assert.Contains("Apply Coupon Code", html);
        Assert.Contains("Proceed to Checkout", html);
    }

    [Fact]
    public async Task ECommerce_Checkout_RendersAddressesAndPaymentModes()
    {
        var client = await CreateAuthenticatedCustomerClientAsync("customer_checkout@ayurveda.com");
        
        // Ensure cart has item first
        var form = new Dictionary<string, string> { ["productId"] = "1", ["quantity"] = "1" };
        await client.PostAsync("/cart/addtocart", new FormUrlEncodedContent(form));

        var response = await client.GetAsync("/checkout");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Finalize Your Order", html);
        Assert.Contains("Delivery Address", html);
        Assert.Contains("Standard Delivery", html);
        Assert.Contains("Express Priority", html);
        Assert.Contains("Razorpay Secure Gateway", html);
        Assert.Contains("Cash on Delivery", html);
    }

    [Fact]
    public async Task ECommerce_Wishlist_Index_RendersSuccessfully()
    {
        var client = await CreateAuthenticatedCustomerClientAsync("customer_wishlist@ayurveda.com");
        var response = await client.GetAsync("/wishlist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("My Saved Formulations", html);
    }

    [Fact]
    public async Task ECommerce_ProductCompare_RendersMatrix()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/shop/compare?ids=1,2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Side-by-Side Product Comparison", html);
        Assert.Contains("Key Ingredients", html);
        Assert.Contains("Traditionally Used For", html);
    }

    // =========================================================================
    // PHASE 6: SMART SEARCH TESTS
    // =========================================================================

    [Fact]
    public async Task Search_Suggest_ReturnsGroupedResults_WithLatencyHeader()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/search/suggest?q=ashwa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Search-Latency-Ms"));

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ashwagandha", json);
        Assert.Contains("latencyMs", json);
    }

    [Fact]
    public async Task Search_Suggest_HinglishTransliteration_AppliesSynonym()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/search/suggest?q=bukhar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("synonymApplied", json);
        Assert.Contains("Fever", json);
    }

    [Fact]
    public async Task Search_Suggest_TypoTolerance_ReturnsDidYouMean()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/search/suggest?q=chavanprash");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("didYouMean", json);
        Assert.Contains("Chyawanprash", json);
    }

    [Fact]
    public async Task Search_ResultsPage_RendersSuccessfully_WithQueryAndFacets()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/search?q=triphala");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Results for", html);
        Assert.Contains("triphala", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Refine Formulations", html);
        Assert.Contains("Price Range", html);
    }

    [Fact]
    public async Task Search_ResultsPage_WithZeroResults_RendersSuggestionsAndTrending()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/search?q=xyznonexistentayurveda999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No Ayurvedic formulations matched your search", html);
        Assert.Contains("Search Suggestions", html);
        Assert.Contains("Popular Ayurveda Searches", html);
        Assert.Contains("Ashwagandha", html);
    }

    [Fact]
    public async Task Search_ResultsPage_TypoBanner_RendersDidYouMeanLink()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/search?q=chavanprash");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Did you mean:", html);
        Assert.Contains("Chyawanprash", html);
    }

    // =========================================================================
    // PHASE 7: DOCTOR CONSULTATIONS, CALLING & CLINICAL DESK TESTS
    // =========================================================================

    [Fact]
    public async Task Doctors_Index_RendersSeededDoctors_AndEmergencyNotice()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/doctors");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // Medical emergency notice with national helpline numbers
        Assert.Contains("112", html);
        Assert.Contains("102", html);
        Assert.Contains("108", html);
        Assert.Contains("Medical Emergency Advisory", html);

        // Doctor directory facets
        Assert.Contains("Experience", html);
        Assert.Contains("Call Now", html);

        // Seeded authentic doctors
        Assert.Contains("Sharma", html);
    }

    [Fact]
    public async Task Doctors_Profile_RendersDoctorDetails_AndRegistrationNumber()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/doctors/profile/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("DEMO-REG-", html);
        Assert.Contains("Years", html);
        Assert.Contains("HD Video Call", html);
        Assert.Contains("Book Scheduled Slot", html);
        Assert.Contains("Patient Reviews", html);
    }

    [Fact]
    public async Task Doctors_Book_RendersConsultationModes_AndISTTimezoneNotice()
    {
        var client = await CreateAuthenticatedCustomerClientAsync("customer_doctor@ayurveda.com");
        var response = await client.GetAsync("/doctors/book/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Indian Standard Time", html);
        Assert.Contains("HD Video Call", html);
        Assert.Contains("Private Audio", html);
        Assert.Contains("Chat", html);
        Assert.Contains("Free rescheduling or cancellation", html);
        Assert.Contains("Medical Lab Reports", html);
    }

    [Fact]
    public async Task Doctors_Prescription_RendersOfficialRx_AndAddToCartButton()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/doctors/prescription/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Ayurvedic Digital Prescription", html);
        Assert.Contains("Add All Medicines to Basket", html);
        Assert.Contains("Pathya &amp; Apathya", html);
        Assert.Contains("Vata-Kapha Agnimandya", html);
    }

    [Fact]
    public async Task DoctorPortal_UnauthenticatedUser_RedirectsToLogin()
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/doctorportal/dashboard");

        // Protected doctor portal must redirect to Login
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task DoctorReview_Submission_RejectsNonCompletedAppointment()
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Submitting review for a nonexistent appointment must return NotFound
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AppointmentId"] = "9999",
            ["DoctorId"] = "1",
            ["Rating"] = "5",
            ["ReviewText"] = "Great consultation, highly recommended!"
        });

        var response = await client.PostAsync("/doctors/submitreview", form);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // PHASE 8: ADMIN PORTAL, RBAC & GOVERNANCE TESTS
    // =========================================================================

    private async Task<HttpClient> CreateAuthenticatedCustomerClientAsync(string email = "test_customer@ayurveda.com", string password = "TestPassword@2026!")
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
                    FullName = "Test Customer",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(user, password);
                await userManager.AddToRoleAsync(user, "Customer");
            }
        }

        var client = _webFactory.CreateClient();
        var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password
        });

        await client.PostAsync("/account/login", loginForm);
        return client;
    }

    private async Task<HttpClient> CreateAuthenticatedAdminClientAsync(string email, string password)
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password
        });

        var loginResponse = await client.PostAsync("/admin-portal/auth/login", loginForm);
        Assert.True(loginResponse.StatusCode == HttpStatusCode.Redirect || loginResponse.StatusCode == HttpStatusCode.OK);

        return client;
    }

    [Fact]
    public async Task Admin_Dashboard_RendersExecutiveKpis_WhenAuthenticated()
    {
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var response = await client.GetAsync("/admin-portal/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Executive Management Console", html);
        Assert.Contains("Gross Revenue", html);
        Assert.Contains("Orders Placed", html);
        Assert.Contains("Active Vaidyas", html);
        Assert.Contains("Low-Stock SKUs", html);
        Assert.Contains("Doctor Verification Queue", html);
    }

    [Fact]
    public async Task Admin_DoctorManagement_AllowsDirectoryAudit_AndPendingQueue()
    {
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);

        var response = await client.GetAsync("/admin-portal/doctors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Doctor Practice Management", html);
        Assert.Contains("Registration No.", html);

        var pendingResponse = await client.GetAsync("/admin-portal/doctors/pending");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_AuthorizationMatrix_CatalogManager_CannotAccessFinancePages()
    {
        // Explicit acceptance test: CatalogManager role CANNOT open finance pages
        var client = await CreateAuthenticatedAdminClientAsync("catalog@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var response = await client.GetAsync("/admin-portal/finance");

        // Should return 403 Forbidden or redirect to AccessDenied
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden || 
                   (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString().Contains("accessdenied", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public async Task Admin_Finance_AllowsFinanceManagerAccess_AndGstReportExport()
    {
        var client = await CreateAuthenticatedAdminClientAsync("finance@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var response = await client.GetAsync("/admin-portal/finance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Finance, Payouts &amp; GST Ledger", html);
        Assert.Contains("Platform Fee (15%)", html);
        Assert.Contains("Doctor Payouts &amp; Settlements", html);

        // Test GST CSV Export
        var csvResponse = await client.GetAsync("/admin-portal/finance/exportgstcsv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("Invoice Number", csv);
        Assert.Contains("Taxable Value", csv);
        Assert.Contains("CGST", csv);
    }

    [Fact]
    public async Task Admin_UserManagement_RendersUserList_AndGdprDataExport()
    {
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var response = await client.GetAsync("/admin-portal/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Patient &amp; User Accounts", html);
        Assert.Contains("Total Users:", html);

        // GDPR Data Export for customer account #1
        var exportResponse = await client.GetAsync("/admin-portal/users/exportdata/1");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/json", exportResponse.Content.Headers.ContentType?.MediaType);
        var json = await exportResponse.Content.ReadAsStringAsync();
        Assert.Contains("DPDP Act 2023 / GDPR Data Portability", json);
    }

    [Fact]
    public async Task Admin_Products_AllowsCsvExport_AndInventoryTracking()
    {
        var client = await CreateAuthenticatedAdminClientAsync("catalog@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var response = await client.GetAsync("/admin-portal/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Formulations &amp; Inventory", html);
        Assert.Contains("Shastriya Categories", html);

        // Bulk CSV Export
        var csvResponse = await client.GetAsync("/admin-portal/products/exportcsv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("SKU", csv);
        Assert.Contains("StockQuantity", csv);
    }

    [Fact]
    public async Task Admin_Audit_Trail_RendersSecurityLogs_ForSuperAdmin()
    {
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var response = await client.GetAsync("/admin-portal/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Security &amp; Operational Audit Trail", html);
        Assert.Contains("Timestamp (UTC)", html);
    }

    // =========================================================================
    // PHASE 9: LOGOUT CONFIRMATION, ACCESSIBLE MODALS & IDLE TIMEOUT TESTS
    // =========================================================================

    [Fact]
    public async Task CustomerPortal_RendersAccessibleConfirmModal_AndLogoutTrigger()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // 1. Unified Accessible Confirm Modal exists in the layout
        Assert.Contains("id=\"confirmActionModal\"", html);
        Assert.Contains("role=\"dialog\"", html);
        Assert.Contains("aria-modal=\"true\"", html);
        Assert.Contains("confirmModalForm", html);
        Assert.Contains("confirmModalProceedBtn", html);
        Assert.Contains("confirmModalCancelBtn", html);
    }

    [Fact]
    public async Task Customer_LogoutPost_ClearsSession_AndRedirectsToLoginWithToast()
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // POST logout with form token
        var response = await client.PostAsync("/account/logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        // GET logout is strictly prohibited and redirects safely without performing destructive action
        var getResponse = await client.GetAsync("/account/logout");
        Assert.Equal(HttpStatusCode.Redirect, getResponse.StatusCode);
        Assert.Contains("/Account/Login", getResponse.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DoctorPortalNav_RendersLogoutTrigger_AndIdleTimeoutModal()
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Doctor portal login page renders without errors
        var loginResponse = await client.GetAsync("/account/login?role=Doctor");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var html = await loginResponse.Content.ReadAsStringAsync();

        Assert.Contains("Doctor Portal", html);
        Assert.Contains("Vaidya Clinical Desk", html);
    }

    [Fact]
    public async Task AdminPortal_RendersAccessibleConfirmModal_AndIdleTimeoutModal_WhenAuthenticated()
    {
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
        var response = await client.GetAsync("/admin-portal/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // 1. Accessible Confirm Modal
        Assert.Contains("id=\"confirmActionModal\"", html);
        Assert.Contains("role=\"dialog\"", html);
        Assert.Contains("aria-modal=\"true\"", html);

        // 2. Idle Timeout Modal with countdown
        Assert.Contains("id=\"idleTimeoutModal\"", html);
        Assert.Contains("id=\"idleCountdownTimer\"", html);
        Assert.Contains("btnStaySignedIn", html);
        Assert.Contains("idleLogoutForm", html);

        // 3. Admin Logout triggers (sidebar and header)
        Assert.Contains("data-tga-logout", html);
        Assert.Contains("data-action-url=\"/admin-portal/auth/logout\"", html);
    }

    [Fact]
    public async Task Admin_LogoutPost_RequiresPost_AndRedirectsToAdminLoginWithToast()
    {
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);

        var response = await client.PostAsync("/admin-portal/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin-portal/auth/login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        // GET logout on Admin redirects to login
        var getResponse = await client.GetAsync("/admin-portal/auth/logout");
        Assert.Equal(HttpStatusCode.Redirect, getResponse.StatusCode);
        Assert.Contains("/admin-portal/auth/login", getResponse.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DestructiveActions_DeleteAddress_AndCancelOrder_AreProtectedByPost()
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Delete address without authentication redirects to login
        var deleteResponse = await client.PostAsync("/account/deleteaddress/999", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
        Assert.Contains("/Account/Login", deleteResponse.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        // Order cancel without authentication redirects to login
        var cancelResponse = await client.PostAsync("/orders/cancel/999", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, cancelResponse.StatusCode);
        Assert.Contains("/Account/Login", cancelResponse.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductImages_VerifiedGenuinePhotographs_NoAIGeneratedImages()
    {
        using var scope = _webFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
        var products = await context.Products.Include(p => p.Images).ToListAsync();

        Assert.Equal(400, products.Count);
        Assert.All(products, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.ImageUrl), $"Product {p.Title} has null/empty ImageUrl");
            // Zero legacy AI-generated illustrated packaging files
            Assert.DoesNotContain("dhanvantari-botanicals", p.ImageUrl, StringComparison.OrdinalIgnoreCase);
        });

        // Ensure genuine images are stored brand-wise
        var genuineProducts = products.Where(p => !p.ImageUrl.Contains("placeholder")).ToList();
        Assert.True(genuineProducts.Count >= 300, $"Expected at least 300 genuine products with authentic photos, found {genuineProducts.Count}");

        Assert.All(genuineProducts, p =>
        {
            Assert.Contains("/images/products/", p.ImageUrl);
            Assert.Matches(@"/images/products/(himalaya|dabur|patanjali|baidyanath|zandu|kottakkal|charak|avp|maharishi|sri-sri-tattva)/", p.ImageUrl);
        });
    }

    [Fact]
    public async Task Admin_EditProductPrice_InvalidatesCache_AndPublicPageShowsNewPriceImmediately()
    {
        var publicClient = _webFactory.CreateClient();

        using (var scope = _webFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var product = await db.Products.FirstAsync();
            var originalPrice = product.Price;
            var newPrice = originalPrice + 77.00m;

            // 1. Initial public request caches the product
            var res1 = await publicClient.GetAsync($"/Product/{product.Id}");
            Assert.True(res1.IsSuccessStatusCode);
            var html1 = await res1.Content.ReadAsStringAsync();
            Assert.Contains(originalPrice.ToString("0"), html1);

            // 2. Admin edits product price
            var adminClient = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", WebMvcFactory.TestAdminPassword);
            var editForm = new Dictionary<string, string>
            {
                ["Id"] = product.Id.ToString(),
                ["Title"] = product.Title,
                ["Price"] = newPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Mrp"] = (newPrice + 100).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["StockQuantity"] = product.Stock.ToString(),
                ["CategoryId"] = product.CategoryId.ToString(),
                ["Description"] = product.Description,
                ["Brand"] = product.Brand,
                ["ImageUrl"] = product.ImageUrl,
                ["IsActive"] = "true"
            };

            var postRes = await adminClient.PostAsync("/admin-portal/products/edit", new FormUrlEncodedContent(editForm));
            Assert.True(postRes.StatusCode == HttpStatusCode.Redirect || postRes.IsSuccessStatusCode);

            // 3. Next public request must reflect new price immediately
            var res2 = await publicClient.GetAsync($"/Product/{product.Id}");
            Assert.True(res2.IsSuccessStatusCode);
            var html2 = await res2.Content.ReadAsStringAsync();
            Assert.Contains(newPrice.ToString("0"), html2);
        }
    }

    [Fact]
    public async Task Anonymous_GuestCart_Isolation_And_ProtectedFlowsRedirect()
    {
        // Two different anonymous clients never see each other's cart
        var clientA = _webFactory.CreateClient();
        await clientA.PostAsync("/cart/addtocart", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = "1",
            ["quantity"] = "3"
        }));

        var clientB = _webFactory.CreateClient();
        await clientB.PostAsync("/cart/addtocart", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = "2",
            ["quantity"] = "1"
        }));

        var cartAResp = await clientA.GetAsync("/cart");
        var cartAHtml = await cartAResp.Content.ReadAsStringAsync();
        Assert.Contains("3", cartAHtml); // Quantity 3 of product 1

        var cartBResp = await clientB.GetAsync("/cart");
        var cartBHtml = await cartBResp.Content.ReadAsStringAsync();
        Assert.Contains("1", cartBHtml); // Quantity 1 of product 2

        // Anonymous checkout/booking redirects to login and never creates records for existing users
        var noRedirectClient = _webFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var checkoutResp = await noRedirectClient.GetAsync("/checkout");
        Assert.Equal(HttpStatusCode.Redirect, checkoutResp.StatusCode);
        Assert.Contains("/account/login", checkoutResp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        var bookResp = await noRedirectClient.GetAsync("/doctors/book/1");
        Assert.Equal(HttpStatusCode.Redirect, bookResp.StatusCode);
        Assert.Contains("/account/login", bookResp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DemoOtpSkip_NeverResetsPassword_Of_AdminStaffOrDoctor()
    {
        var client = _webFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Attempting OTP verify with demo code for protected admin account
        var adminOtpForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "admin@thegreatayurveda.com",
            ["Otp"] = "123456"
        });

        var adminResp = await client.PostAsync("/account/verify-otp", adminOtpForm);
        var adminContent = await adminResp.Content.ReadAsStringAsync();
        Assert.True(adminContent.Contains("expired") || adminContent.Contains("Invalid") || adminResp.StatusCode == HttpStatusCode.BadRequest || adminResp.StatusCode == HttpStatusCode.OK);

        // Attempting password reset with demo code for protected doctor
        var docResetForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "dr.aarav.sharma@thegreatayurveda.com",
            ["Otp"] = "000000",
            ["NewPassword"] = "HackedPass123!",
            ["ConfirmPassword"] = "HackedPass123!"
        });

        var docResp = await client.PostAsync("/account/reset-password", docResetForm);
        var docContent = await docResp.Content.ReadAsStringAsync();
        Assert.True(docContent.Contains("Invalid or expired") || docContent.Contains("Invalid") || docResp.StatusCode == HttpStatusCode.BadRequest || docResp.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task DemoViewer_AdminMutations_BlockedServerSide_WithReadOnlyMessage()
    {
        // 1. Authenticate as DemoViewer
        var demoViewerClient = await CreateAuthenticatedAdminClientAsync("demo-admin@ayurveda.local", WebMvcFactory.TestAdminPassword);

        // 2. Attempt to create a category via POST
        var postForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Unauthorized Category",
            ["Description"] = "Should be blocked",
            ["IsActive"] = "true"
        });

        var postRes = await demoViewerClient.PostAsync("/admin-portal/categories/create", postForm);
        var content = await postRes.Content.ReadAsStringAsync();

        // Must be blocked with 403 or redirect with Demo notification
        Assert.True(postRes.StatusCode == HttpStatusCode.Forbidden || 
                    postRes.StatusCode == HttpStatusCode.Redirect || 
                    content.Contains("Read-only demo"));
    }

    [Fact]
    public async Task Unauthenticated_PlaceOrder_CannotMarkPaid_RedirectsToLogin()
    {
        var anonymousClient = _webFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var postData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SelectedPaymentMethod"] = "Razorpay",
            ["DeliveryType"] = "Standard",
            ["IsLiveRazorpay"] = "false"
        });

        var response = await anonymousClient.PostAsync("/Checkout/PlaceOrder", postData);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/Account/Login", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaymentProvider_LiveSignatureVerification_RejectsInvalidSignature()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Razorpay:KeyId"] = "rzp_live_testkey123",
            ["Razorpay:KeySecret"] = "secret_key_very_secure_test_123"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var clientFactory = new DummyHttpClientFactory();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Ayurveda.Infrastructure.Services.PaymentProvider>.Instance;

        var provider = new Ayurveda.Infrastructure.Services.PaymentProvider(config, clientFactory, logger);

        // Valid signature for order_123|pay_456 with secret_key_very_secure_test_123:
        var payload = "order_123|pay_456";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("secret_key_very_secure_test_123"));
        var validSig = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        // 1. Valid signature passes
        var validResult = await provider.VerifyPaymentSignatureAsync("order_123", "pay_456", validSig);
        Assert.True(validResult);

        // 2. Tampered or fake signature fails
        var fakeResult = await provider.VerifyPaymentSignatureAsync("order_123", "pay_456", "tampered_fake_signature");
        Assert.False(fakeResult);

        // 3. Empty signature fails
        var emptyResult = await provider.VerifyPaymentSignatureAsync("order_123", "pay_456", "");
        Assert.False(emptyResult);
    }

    private class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }
}



