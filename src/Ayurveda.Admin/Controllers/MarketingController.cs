using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Policy = "RequireCatalogManagement")]
public class MarketingController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;

    public MarketingController(AyurvedaDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // -------------------------------------------------------------------------
    // 1. COUPONS & PROMOTIONAL DISCOUNTS
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Coupons()
    {
        var coupons = await _context.Coupons.OrderByDescending(c => c.Id).ToListAsync();
        return View(coupons);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCoupon(string code, string discountType, decimal discountAmount, decimal minOrderAmount, decimal maxDiscountAmount, DateTime? expiryDate)
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
            ExpiryDate = expiryDate,
            IsActive = true,
            UsageLimit = 1000,
            UsageCount = 0
        };

        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("CouponCreated", "Coupon", coupon.Id.ToString(), $"Created coupon {coupon.Code} ({discountType} {discountAmount})", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Coupon {coupon.Code} created successfully.";
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
            TempData["SuccessMessage"] = $"Coupon {coupon.Code} is now {(coupon.IsActive ? "Active" : "Disabled")}.";
        }
        return RedirectToAction(nameof(Coupons));
    }

    // -------------------------------------------------------------------------
    // 2. HOMEPAGE BANNERS
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Banners()
    {
        var banners = await _context.Banners.OrderBy(b => b.DisplayOrder).ToListAsync();
        return View(banners);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBanner(string title, string? subtitle, string imageUrl, string? linkUrl, int displayOrder)
    {
        var banner = new Banner
        {
            Title = title,
            Subtitle = subtitle,
            ImageUrl = imageUrl,
            LinkUrl = linkUrl,
            DisplayOrder = displayOrder,
            IsActive = true
        };

        _context.Banners.Add(banner);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("BannerCreated", "Banner", banner.Id.ToString(), $"Created promo banner '{title}'", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Banner '{title}' created.";
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
            TempData["SuccessMessage"] = "Banner deleted.";
        }
        return RedirectToAction(nameof(Banners));
    }
}
