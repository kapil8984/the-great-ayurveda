using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class CartController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CartController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<ApplicationUser> GetOrCreateUserCartUserAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            var demoUser = await _context.Users.FirstOrDefaultAsync();
            if (demoUser == null)
            {
                demoUser = new ApplicationUser
                {
                    UserName = "guest@ayurveda.local",
                    Email = "guest@ayurveda.local",
                    FullName = "Guest Customer",
                    EmailConfirmed = true
                };
                await _userManager.CreateAsync(demoUser, "GuestPass@2026");
            }
            user = demoUser;
        }
        return user;
    }

    private async Task<Cart> GetOrCreateUserCartAsync()
    {
        var user = await GetOrCreateUserCartUserAsync();

        var cart = await _context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (cart == null)
        {
            cart = new Cart { UserId = user.Id };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        return cart;
    }

    public async Task<IActionResult> Index()
    {
        var user = await GetOrCreateUserCartUserAsync();
        var cart = await GetOrCreateUserCartAsync();

        var appliedCouponCode = HttpContext.Session.GetString("AppliedCouponCode");
        decimal couponDiscount = 0;
        string? couponSuccess = null;
        string? couponError = null;

        var items = cart.CartItems.Select(ci => new CartItemViewModel
        {
            CartItemId = ci.Id,
            ProductId = ci.ProductId,
            Title = ci.Product.Title,
            Brand = ci.Product.Brand,
            ImageUrl = ci.Product.ImageUrl,
            UnitPrice = ci.Product.Price,
            Mrp = ci.Product.Mrp,
            Quantity = ci.Quantity,
            Stock = ci.Product.Stock
        }).ToList();

        decimal subtotal = items.Sum(i => i.TotalPrice);

        if (!string.IsNullOrWhiteSpace(appliedCouponCode))
        {
            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code.ToUpper() == appliedCouponCode.ToUpper() && c.IsActive);
            if (coupon != null)
            {
                if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTime.UtcNow)
                {
                    couponError = "This coupon code has expired.";
                    HttpContext.Session.Remove("AppliedCouponCode");
                }
                else if (subtotal < coupon.MinOrderAmount)
                {
                    couponError = $"Coupon requires minimum cart value of ₹{coupon.MinOrderAmount:N0}.";
                }
                else
                {
                    if (coupon.DiscountType == DiscountType.Percentage)
                    {
                        var calc = Math.Round(subtotal * (coupon.DiscountAmount / 100m), 2);
                        couponDiscount = coupon.MaxDiscountAmount > 0 ? Math.Min(calc, coupon.MaxDiscountAmount) : calc;
                    }
                    else
                    {
                        couponDiscount = coupon.DiscountAmount;
                    }
                    couponSuccess = $"Coupon '{coupon.Code}' applied successfully! Saved ₹{couponDiscount:N0}.";
                }
            }
            else
            {
                couponError = "Invalid coupon code.";
                HttpContext.Session.Remove("AppliedCouponCode");
            }
        }

        // Saved for later items (from Wishlist)
        var savedItems = await _context.WishlistItems
            .Include(w => w.Product)
            .Where(w => w.UserId == user.Id && w.Product.IsActive)
            .OrderByDescending(w => w.CreatedAt)
            .Take(5)
            .Select(w => new CartItemViewModel
            {
                CartItemId = w.Id,
                ProductId = w.ProductId,
                Title = w.Product.Title,
                Brand = w.Product.Brand,
                ImageUrl = w.Product.ImageUrl,
                UnitPrice = w.Product.Price,
                Mrp = w.Product.Mrp,
                Quantity = 1,
                Stock = w.Product.Stock
            })
            .ToListAsync();

        var model = new CartViewModel
        {
            CartId = cart.Id,
            Items = items,
            SavedForLaterItems = savedItems,
            AppliedCouponCode = appliedCouponCode,
            CouponDiscount = couponDiscount,
            CouponSuccess = couponSuccess,
            CouponError = couponError
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        var cart = await GetOrCreateUserCartAsync();
        var product = await _context.Products.FindAsync(productId);

        if (product == null || !product.IsActive)
        {
            TempData["ErrorMessage"] = "Product is no longer available.";
            return RedirectToAction(nameof(Index));
        }

        var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
        if (item != null)
        {
            item.Quantity = Math.Min(product.Stock, item.Quantity + quantity);
        }
        else
        {
            cart.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                ProductId = productId,
                Quantity = Math.Min(product.Stock, Math.Max(1, quantity))
            });
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Added '{product.Title}' to your basket!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyNow(int productId, int quantity = 1)
    {
        var cart = await GetOrCreateUserCartAsync();
        var product = await _context.Products.FindAsync(productId);

        if (product == null || !product.IsActive)
        {
            TempData["ErrorMessage"] = "Product is unavailable.";
            return RedirectToAction("Index", "Shop");
        }

        var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
        if (item != null)
        {
            item.Quantity = Math.Max(1, quantity);
        }
        else
        {
            cart.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = productId, Quantity = Math.Max(1, quantity) });
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("Index", "Checkout");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCartAjax(int productId, int quantity = 1)
    {
        var cart = await GetOrCreateUserCartAsync();
        var product = await _context.Products.FindAsync(productId);

        if (product == null || !product.IsActive)
        {
            return Json(new { success = false, message = "Product is currently unavailable." });
        }

        var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
        if (item != null)
        {
            item.Quantity = Math.Min(product.Stock, item.Quantity + quantity);
        }
        else
        {
            cart.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = productId, Quantity = Math.Min(product.Stock, Math.Max(1, quantity)) });
        }

        await _context.SaveChangesAsync();
        var totalCount = cart.CartItems.Sum(ci => ci.Quantity);
        return Json(new { success = true, cartCount = totalCount, message = $"Added '{product.Title}' to basket!" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
    {
        var item = await _context.CartItems.Include(ci => ci.Product).FirstOrDefaultAsync(ci => ci.Id == cartItemId);
        if (item != null)
        {
            if (quantity <= 0)
            {
                _context.CartItems.Remove(item);
                TempData["SuccessMessage"] = "Item removed from basket.";
            }
            else
            {
                if (quantity > item.Product.Stock)
                {
                    TempData["ErrorMessage"] = $"Only {item.Product.Stock} units currently available in stock.";
                    item.Quantity = item.Product.Stock;
                }
                else
                {
                    item.Quantity = quantity;
                }
            }
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int cartItemId)
    {
        var item = await _context.CartItems.FindAsync(cartItemId);
        if (item != null)
        {
            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Item removed from basket.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveForLater(int cartItemId)
    {
        var user = await GetOrCreateUserCartUserAsync();
        var item = await _context.CartItems.FindAsync(cartItemId);

        if (item != null)
        {
            // Add to Wishlist
            var existingWish = await _context.WishlistItems.FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == item.ProductId);
            if (existingWish == null)
            {
                _context.WishlistItems.Add(new WishlistItem
                {
                    UserId = user.Id,
                    ProductId = item.ProductId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Remove from Cart
            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Item saved for later in your wishlist.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon(string couponCode)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            TempData["ErrorMessage"] = "Please enter a coupon code.";
            return RedirectToAction(nameof(Index));
        }

        var normalized = couponCode.Trim().ToUpper();
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code.ToUpper() == normalized && c.IsActive);

        if (coupon == null)
        {
            TempData["ErrorMessage"] = $"Coupon code '{couponCode}' is invalid or expired.";
            return RedirectToAction(nameof(Index));
        }

        HttpContext.Session.SetString("AppliedCouponCode", normalized);
        TempData["SuccessMessage"] = $"Promo code '{normalized}' applied!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveCoupon()
    {
        HttpContext.Session.Remove("AppliedCouponCode");
        TempData["SuccessMessage"] = "Coupon code removed.";
        return RedirectToAction(nameof(Index));
    }
}
