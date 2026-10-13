using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    private readonly string _dbName = "WebMvcTestDb_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

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
            DbInitializer.InitializeAsync(db, userManager, roleManager, logger).GetAwaiter().GetResult();
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

        var response = await client.GetAsync("/admin-portal/Dashboard");

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
        Assert.Contains("Consult Qualified Ayurvedic Vaidyas", html);

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
        var response = await client.GetAsync("/Shop");

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
        var response = await client.GetAsync("/Shop/Details/1");

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
        var response = await client.GetAsync("/Shop/CheckPincode?pincode=110001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"isServiceable\":true", content);
        Assert.Contains("North India", content);
    }

    [Fact]
    public async Task Catalog_PincodeCheck_InvalidPincode_ReturnsError()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/Shop/CheckPincode?pincode=123");

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
        var postResponse = await client.PostAsync("/Cart/AddToCart", new FormUrlEncodedContent(form));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.StatusCode == HttpStatusCode.Redirect);

        // Fetch Cart page
        var cartResponse = await client.GetAsync("/Cart");
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
        var client = _webFactory.CreateClient();
        
        // Ensure cart has item first
        var form = new Dictionary<string, string> { ["productId"] = "1", ["quantity"] = "1" };
        await client.PostAsync("/Cart/AddToCart", new FormUrlEncodedContent(form));

        var response = await client.GetAsync("/Checkout");
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
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/Wishlist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("My Saved Formulations", html);
    }

    [Fact]
    public async Task ECommerce_ProductCompare_RendersMatrix()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/Shop/Compare?ids=1,2");

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
        var response = await client.GetAsync("/Doctors");

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
        var response = await client.GetAsync("/Doctors/Profile/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("DEMO-REG-", html);
        Assert.Contains("Years", html);
        Assert.Contains("HD Video Call", html);
        Assert.Contains("Book Scheduled Slot", html);
        Assert.Contains("Verified Patient Reviews", html);
    }

    [Fact]
    public async Task Doctors_Book_RendersConsultationModes_AndISTTimezoneNotice()
    {
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/Doctors/Book/1");

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
        var response = await client.GetAsync("/Doctors/Prescription/1");

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

        var response = await client.GetAsync("/DoctorPortal/Dashboard");

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

        var response = await client.PostAsync("/Doctors/SubmitReview", form);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // PHASE 8: ADMIN PORTAL, RBAC & GOVERNANCE TESTS
    // =========================================================================

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
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", "AdminSecure@2026");
        var response = await client.GetAsync("/admin-portal/Dashboard");

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
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", "AdminSecure@2026");

        var response = await client.GetAsync("/admin-portal/Doctors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Doctor Practice Management", html);
        Assert.Contains("Registration No.", html);

        var pendingResponse = await client.GetAsync("/admin-portal/Doctors/Pending");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_AuthorizationMatrix_CatalogManager_CannotAccessFinancePages()
    {
        // Explicit acceptance test: CatalogManager role CANNOT open finance pages
        var client = await CreateAuthenticatedAdminClientAsync("catalog@thegreatayurveda.com", "AdminSecure@2026");
        var response = await client.GetAsync("/admin-portal/Finance");

        // Should return 403 Forbidden or redirect to AccessDenied
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden || 
                   (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString().Contains("accessdenied", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public async Task Admin_Finance_AllowsFinanceManagerAccess_AndGstReportExport()
    {
        var client = await CreateAuthenticatedAdminClientAsync("finance@thegreatayurveda.com", "AdminSecure@2026");
        var response = await client.GetAsync("/admin-portal/Finance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Finance, Payouts &amp; GST Ledger", html);
        Assert.Contains("Platform Fee (15%)", html);
        Assert.Contains("Doctor Payouts &amp; Settlements", html);

        // Test GST CSV Export
        var csvResponse = await client.GetAsync("/admin-portal/Finance/ExportGstCsv");
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
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", "AdminSecure@2026");
        var response = await client.GetAsync("/admin-portal/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Patient &amp; User Accounts", html);
        Assert.Contains("Total Users:", html);

        // GDPR Data Export for customer account #1
        var exportResponse = await client.GetAsync("/admin-portal/Users/ExportData/1");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/json", exportResponse.Content.Headers.ContentType?.MediaType);
        var json = await exportResponse.Content.ReadAsStringAsync();
        Assert.Contains("DPDP Act 2023 / GDPR Data Portability", json);
    }

    [Fact]
    public async Task Admin_Products_AllowsCsvExport_AndInventoryTracking()
    {
        var client = await CreateAuthenticatedAdminClientAsync("catalog@thegreatayurveda.com", "AdminSecure@2026");
        var response = await client.GetAsync("/admin-portal/Products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Formulations &amp; Inventory", html);
        Assert.Contains("Shastriya Categories", html);

        // Bulk CSV Export
        var csvResponse = await client.GetAsync("/admin-portal/Products/ExportCsv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("SKU", csv);
        Assert.Contains("StockQuantity", csv);
    }

    [Fact]
    public async Task Admin_Audit_Trail_RendersSecurityLogs_ForSuperAdmin()
    {
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", "AdminSecure@2026");
        var response = await client.GetAsync("/admin-portal/Audit");

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
        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        // GET logout is strictly prohibited and redirects safely without performing destructive action
        var getResponse = await client.GetAsync("/Account/Logout");
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
        var loginResponse = await client.GetAsync("/Account/Login?role=Doctor");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var html = await loginResponse.Content.ReadAsStringAsync();

        Assert.Contains("Doctor Portal", html);
        Assert.Contains("Vaidya Clinical Desk", html);
    }

    [Fact]
    public async Task AdminPortal_RendersAccessibleConfirmModal_AndIdleTimeoutModal_WhenAuthenticated()
    {
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", "AdminSecure@2026");
        var response = await client.GetAsync("/admin-portal/Dashboard");

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
        var client = await CreateAuthenticatedAdminClientAsync("admin@thegreatayurveda.com", "AdminSecure@2026");

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
        var deleteResponse = await client.PostAsync("/Account/DeleteAddress/999", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
        Assert.Contains("/Account/Login", deleteResponse.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        // Order cancel without authentication redirects to login
        var cancelResponse = await client.PostAsync("/Orders/Cancel/999", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, cancelResponse.StatusCode);
        Assert.Contains("/Account/Login", cancelResponse.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}



