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
}

public class AdminMvcFactory : WebApplicationFactory<Ayurveda.Admin.Program>
{
    private readonly string _dbName = "AdminMvcTestDb_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
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

            services.AddDbContext<AyurvedaDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }
}

public class WebMvcTests : IClassFixture<WebMvcFactory>, IClassFixture<AdminMvcFactory>
{
    private readonly WebMvcFactory _webFactory;
    private readonly AdminMvcFactory _adminFactory;

    public WebMvcTests(WebMvcFactory webFactory, AdminMvcFactory adminFactory)
    {
        _webFactory = webFactory;
        _adminFactory = adminFactory;
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
        var client = _adminFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Dashboard");

        // Protected route must redirect to Admin Login (/Auth/Login)
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Auth/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AdminLogin_RendersSecureAdminPage()
    {
        var client = _adminFactory.CreateClient();
        var response = await client.GetAsync("/Auth/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("The Great Ayurveda Admin Portal", content);
        Assert.Contains("RESTRICTED ACCESS", content);
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
        var client = _adminFactory.CreateClient();
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
        Assert.Contains("AYUSH15", html);
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
        Assert.Contains("Consult Certified Ayurvedic Vaidyas", html);

        // Section 9: 3-step timeline
        Assert.Contains("How The Great Ayurveda Works", html);

        // Section 10: Dinacharya routine clock & Dravya spotlight
        Assert.Contains("id=\"dinacharya\"", html);
        Assert.Contains("Brahma Muhurta", html);

        // Section 11: Quality promises & FAQ accordion
        Assert.Contains("Heavy-Metal Tested", html);
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
        var client = _adminFactory.CreateClient();
        var response = await client.GetAsync("/auth/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("RESTRICTED ACCESS", html);
        Assert.Contains("TotpCode", html);
        Assert.Contains("id=\"toggleAdminPwdBtn\"", html);
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
        Assert.Contains("Great Ayurveda Labs", html);
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
}

