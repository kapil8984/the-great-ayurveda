using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class HomeController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IConfiguration _configuration;

    public HomeController(AyurvedaDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel
        {
            HeroVariant = _configuration["HomeHeroVariant"] ?? "VaidyaHeritage"
        };

        var now = DateTime.UtcNow;
        ViewBag.HeroBanners = await _context.Banners
            .Where(b => b.IsActive &&
                        (b.Placement == "home_hero" || string.IsNullOrEmpty(b.Placement)) &&
                        (b.StartDate == null || b.StartDate <= now) &&
                        (b.EndDate == null || b.EndDate >= now))
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();

        ViewBag.MiddleBanners = await _context.Banners
            .Where(b => b.IsActive &&
                        b.Placement == "home_middle" &&
                        (b.StartDate == null || b.StartDate <= now) &&
                        (b.EndDate == null || b.EndDate >= now))
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();

        // 1. Categories
        model.Categories = await _context.Categories
            .Where(c => c.IsActive)
            .Select(c => new CategoryCardViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ImageUrl = c.ImageUrl ?? "/images/categories/immunity.svg",
                ProductCount = c.Products.Count(p => p.IsActive)
            })
            .ToListAsync();

        // 2. Featured Products
        model.FeaturedProducts = await _context.Products
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.AverageRating)
            .Take(8)
            .Select(p => new ProductCardViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Brand = p.Brand,
                CategoryName = p.Category.Name,
                Price = p.Price,
                Mrp = p.Mrp,
                DiscountPercent = p.DiscountPercent,
                ImageUrl = p.ImageUrl,
                AverageRating = p.AverageRating,
                Stock = p.Stock,
                Badge = p.AverageRating >= 4.8 ? "Bestseller" : "Classical"
            })
            .ToListAsync();

        // 3. Deals (High discount products for Deals Strip)
        model.Deals = await _context.Products
            .Where(p => p.IsActive && p.DiscountPercent > 0)
            .OrderByDescending(p => p.DiscountPercent)
            .Take(4)
            .Select(p => new ProductCardViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Brand = p.Brand,
                CategoryName = p.Category.Name,
                Price = p.Price,
                Mrp = p.Mrp,
                DiscountPercent = p.DiscountPercent,
                ImageUrl = p.ImageUrl,
                AverageRating = p.AverageRating,
                Stock = p.Stock,
                Badge = $"{p.DiscountPercent}% OFF"
            })
            .ToListAsync();

        // 4. Top Doctors (Approved only)
        model.TopDoctors = await _context.Doctors
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1)
            .Take(4)
            .Select(d => new DoctorCardViewModel
            {
                Id = d.Id,
                DoctorName = d.DoctorName,
                SpecializationName = d.DoctorDetail!.Specialization.Name,
                ExperienceYears = d.DoctorDetail.Experience,
                Qualification = d.DoctorDetail.Qualification,
                Languages = d.DoctorDetail.Language,
                ConsultationFee = d.DoctorDetail.ConsultationFee,
                Rating = d.DoctorDetail.Rating,
                ReviewCount = d.DoctorDetail.ReviewCount,
                IsOnline = d.DoctorDetail.IsOnline,
                ProfileImage = d.ProfileImage
            })
            .ToListAsync();

        return View(model);
    }

    public IActionResult About()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Contact()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Contact(string? name, string? email, string? subject, string? message)
    {
        ViewBag.SubmissionNotice = "Notice: This website is a demonstration platform. Your message has NOT been delivered, processed, or stored, and this mailbox is unmonitored.";
        return View();
    }

    public IActionResult Faq()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Terms()
    {
        return View();
    }

    [HttpGet]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Refunds()
    {
        return View();
    }

    [HttpGet]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Shipping()
    {
        return View();
    }

    [HttpGet("home/notfound")]
    [HttpPost("home/notfound")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult NotFoundPage()
    {
        Response.StatusCode = 404;
        return View("NotFound");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewBag.RequestId = HttpContext.TraceIdentifier;
        return View();
    }

    [HttpHead, HttpGet, Route("ping")]
    public IActionResult Ping()
    {
        return Ok();
    }
}
