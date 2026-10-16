using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookieScheme", Policy = "RequireCatalogManagement")]
public class MarketingController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditService _auditService;
    private readonly ICacheBuster _cacheBuster;

    public MarketingController(AyurvedaDbContext context, IFileStorageService fileStorage, IAuditService auditService, ICacheBuster cacheBuster)
    {
        _context = context;
        _fileStorage = fileStorage;
        _auditService = auditService;
        _cacheBuster = cacheBuster;
    }

    // -------------------------------------------------------------------------
    // 1. COUPONS & PROMOTIONAL DISCOUNTS
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Coupons()
    {
        var coupons = await _context.Coupons.OrderByDescending(c => c.Id).ToListAsync();
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
        return View(coupons);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCoupon(string code, string discountType, decimal discountAmount, decimal minOrderAmount, decimal maxDiscountAmount, string? applicableCategory, DateTime? startDate, DateTime? expiryDate)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["ErrorMessage"] = "Coupon code is required.";
            return RedirectToAction(nameof(Coupons));
        }

        var isFlat = discountType.Equals("Flat", StringComparison.OrdinalIgnoreCase);

        var coupon = new Coupon
        {
            Code = code.Trim().ToUpper(),
            DiscountType = isFlat ? DiscountType.Flat : DiscountType.Percentage,
            DiscountAmount = discountAmount,
            MinOrderAmount = minOrderAmount,
            MaxDiscountAmount = maxDiscountAmount,
            ApplicableCategory = string.IsNullOrWhiteSpace(applicableCategory) ? null : applicableCategory.Trim(),
            StartDate = startDate,
            ExpiryDate = expiryDate,
            IsActive = true,
            UsageLimit = 1000,
            UsageCount = 0
        };

        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("CouponCreated", "Coupon", coupon.Id.ToString(), $"Created coupon {coupon.Code} ({discountType} {discountAmount})", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
        _cacheBuster.InvalidateCoupon();

        TempData["SuccessMessage"] = $"Coupon {coupon.Code} created successfully.";
        return RedirectToAction(nameof(Coupons));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCoupon(int id, string code, string discountType, decimal discountAmount, decimal minOrderAmount, decimal maxDiscountAmount, string? applicableCategory, DateTime? startDate, DateTime? expiryDate, bool isActive)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon == null) return NotFound();

        var isFlat = discountType.Equals("Flat", StringComparison.OrdinalIgnoreCase);
        coupon.Code = code.Trim().ToUpper();
        coupon.DiscountType = isFlat ? DiscountType.Flat : DiscountType.Percentage;
        coupon.DiscountAmount = discountAmount;
        coupon.MinOrderAmount = minOrderAmount;
        coupon.MaxDiscountAmount = maxDiscountAmount;
        coupon.ApplicableCategory = string.IsNullOrWhiteSpace(applicableCategory) ? null : applicableCategory.Trim();
        coupon.StartDate = startDate;
        coupon.ExpiryDate = expiryDate;
        coupon.IsActive = isActive;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("CouponUpdated", "Coupon", coupon.Id.ToString(), $"Updated coupon {coupon.Code}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
        _cacheBuster.InvalidateCoupon();

        TempData["SuccessMessage"] = $"Coupon {coupon.Code} updated successfully.";
        return RedirectToAction(nameof(Coupons));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCoupon(int id)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon != null)
        {
            coupon.IsActive = !coupon.IsActive;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("CouponToggled", "Coupon", id.ToString(), $"Toggled coupon {coupon.Code} active state to {coupon.IsActive}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            _cacheBuster.InvalidateCoupon();
            TempData["SuccessMessage"] = $"Coupon {coupon.Code} is now {(coupon.IsActive ? "Active" : "Disabled")}.";
        }
        return RedirectToAction(nameof(Coupons));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCoupon(int id)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon != null)
        {
            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("CouponDeleted", "Coupon", id.ToString(), $"Deleted coupon {coupon.Code}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            _cacheBuster.InvalidateCoupon();
            TempData["SuccessMessage"] = $"Coupon {coupon.Code} deleted.";
        }
        return RedirectToAction(nameof(Coupons));
    }

    // -------------------------------------------------------------------------
    // 2. STORE BANNERS
    // -------------------------------------------------------------------------
    [HttpGet]
    [Route("admin-portal/Banners")]
    [Route("admin-portal/Marketing/Banners")]
    public async Task<IActionResult> Banners()
    {
        var banners = await _context.Banners.OrderBy(b => b.DisplayOrder).ToListAsync();
        return View(banners);
    }

    [HttpPost]
    [Route("admin-portal/CreateBanner")]
    [Route("admin-portal/Marketing/CreateBanner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBanner(string title, string? subtitle, string? imageUrl, string? linkUrl, string placement, int displayOrder, DateTime? startDate, DateTime? endDate, IFormFile? imageFile)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["ErrorMessage"] = "Banner title is required.";
            return RedirectToAction(nameof(Banners));
        }

        string finalImageUrl = imageUrl ?? string.Empty;
        if (imageFile != null && imageFile.Length > 0)
        {
            using var stream = imageFile.OpenReadStream();
            finalImageUrl = await _fileStorage.SaveFileAsync(stream, imageFile.FileName, imageFile.ContentType);
        }
        else if (string.IsNullOrWhiteSpace(finalImageUrl))
        {
            finalImageUrl = "/images/banners/banner-diwali.svg";
        }

        var banner = new Banner
        {
            Title = title.Trim(),
            Subtitle = subtitle?.Trim(),
            ImageUrl = finalImageUrl,
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? "/Shop" : linkUrl.Trim(),
            Placement = string.IsNullOrWhiteSpace(placement) ? "home_hero" : placement.Trim().ToLowerInvariant(),
            DisplayOrder = displayOrder,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true
        };

        _context.Banners.Add(banner);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("BannerCreated", "Banner", banner.Id.ToString(), $"Created promotional banner '{title}' (Placement: {banner.Placement})", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
        _cacheBuster.InvalidateBanner();

        TempData["SuccessMessage"] = $"Banner '{title}' created successfully.";
        return RedirectToAction(nameof(Banners));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBanner(int id, string title, string? subtitle, string? imageUrl, string? linkUrl, string placement, int displayOrder, DateTime? startDate, DateTime? endDate, bool isActive, IFormFile? imageFile)
    {
        var banner = await _context.Banners.FindAsync(id);
        if (banner == null) return NotFound();

        banner.Title = title.Trim();
        banner.Subtitle = subtitle?.Trim();
        banner.LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? "/Shop" : linkUrl.Trim();
        banner.Placement = string.IsNullOrWhiteSpace(placement) ? "home_hero" : placement.Trim().ToLowerInvariant();
        banner.DisplayOrder = displayOrder;
        banner.StartDate = startDate;
        banner.EndDate = endDate;
        banner.IsActive = isActive;

        if (imageFile != null && imageFile.Length > 0)
        {
            using var stream = imageFile.OpenReadStream();
            banner.ImageUrl = await _fileStorage.SaveFileAsync(stream, imageFile.FileName, imageFile.ContentType);
        }
        else if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            banner.ImageUrl = imageUrl.Trim();
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("BannerUpdated", "Banner", banner.Id.ToString(), $"Updated promotional banner '{title}'", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
        _cacheBuster.InvalidateBanner();

        TempData["SuccessMessage"] = $"Banner '{title}' updated successfully.";
        return RedirectToAction(nameof(Banners));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBanner(int id)
    {
        var banner = await _context.Banners.FindAsync(id);
        if (banner != null)
        {
            banner.IsActive = !banner.IsActive;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("BannerToggled", "Banner", id.ToString(), $"Toggled banner '{banner.Title}' to active={banner.IsActive}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            _cacheBuster.InvalidateBanner();
            TempData["SuccessMessage"] = $"Banner '{banner.Title}' is now {(banner.IsActive ? "Active" : "Disabled")}.";
        }
        return RedirectToAction(nameof(Banners));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        var banner = await _context.Banners.FindAsync(id);
        if (banner != null)
        {
            _context.Banners.Remove(banner);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("BannerDeleted", "Banner", id.ToString(), $"Removed banner #{id}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            _cacheBuster.InvalidateBanner();
            TempData["SuccessMessage"] = "Banner deleted.";
        }
        return RedirectToAction(nameof(Banners));
    }
}
