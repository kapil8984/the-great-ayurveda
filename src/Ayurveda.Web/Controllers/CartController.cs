using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class CartController : Controller
{
    private const string GuestCartSessionKey = "GuestCart_Items";
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _cache;

    public class GuestCartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public CartController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager, IMemoryCache cache)
    {
        _context = context;
        _userManager = userManager;
        _cache = cache;
    }

    private async Task<ApplicationUser?> GetAuthenticatedUserAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return await _userManager.GetUserAsync(User);
        }
        return null;
    }

    private List<GuestCartItemDto> GetGuestCart()
    {
        var json = HttpContext.Session.GetString(GuestCartSessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<GuestCartItemDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<GuestCartItemDto>>(json) ?? new List<GuestCartItemDto>();
        }
        catch
        {
            return new List<GuestCartItemDto>();
        }
    }

    private void SaveGuestCart(List<GuestCartItemDto> items)
    {
        var json = JsonSerializer.Serialize(items);
        HttpContext.Session.SetString(GuestCartSessionKey, json);
    }

    private async Task<Cart?> GetOrCreateUserCartAsync(int userId)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        return cart;
    }

    public async Task<IActionResult> Index()
    {
        var user = await GetAuthenticatedUserAsync();
        List<CartItemViewModel> items = new();
        int cartId = 0;

        if (user != null)
        {
            var cart = await GetOrCreateUserCartAsync(user.Id);
            if (cart != null)
            {
                cartId = cart.Id;
                items = cart.CartItems.Select(ci => new CartItemViewModel
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
            }
        }
        else
        {
            var guestItems = GetGuestCart();
            if (guestItems.Any())
            {
                var productIds = guestItems.Select(gi => gi.ProductId).Distinct().ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id) && p.IsActive)
                    .ToDictionaryAsync(p => p.Id);

                foreach (var gi in guestItems)
                {
                    if (products.TryGetValue(gi.ProductId, out var product))
                    {
                        items.Add(new CartItemViewModel
                        {
                            CartItemId = gi.ProductId,
                            ProductId = gi.ProductId,
                            Title = product.Title,
                            Brand = product.Brand,
                            ImageUrl = product.ImageUrl,
                            UnitPrice = product.Price,
                            Mrp = product.Mrp,
                            Quantity = Math.Min(product.Stock, Math.Max(1, gi.Quantity)),
                            Stock = product.Stock
                        });
                    }
                }
            }
        }

        var appliedCouponCode = HttpContext.Session.GetString("AppliedCouponCode");
        decimal couponDiscount = 0;
        string? couponSuccess = null;
        string? couponError = null;
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

        List<CartItemViewModel> savedItems = new();
        if (user != null)
        {
            savedItems = await _context.WishlistItems
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
        }

        var model = new CartViewModel
        {
            CartId = cartId,
            Items = items,
            SavedForLaterItems = savedItems,
            AppliedCouponCode = appliedCouponCode,
            CouponDiscount = couponDiscount,
            CouponSuccess = couponSuccess,
            CouponError = couponError
        };

        ViewBag.CartBanners = await _cache.GetOrCreateAsync("Cart_Banners", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await _context.Banners
                .AsNoTracking()
                .Where(b => b.IsActive && b.Placement == "cart")
                .OrderBy(b => b.DisplayOrder)
                .ToListAsync();
        });

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null || !product.IsActive)
        {
            TempData["ErrorMessage"] = "Product is no longer available.";
            return RedirectToAction(nameof(Index));
        }

        var user = await GetAuthenticatedUserAsync();
        if (user != null)
        {
            var cart = await GetOrCreateUserCartAsync(user.Id);
            if (cart != null)
            {
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
            }
        }
        else
        {
            var guestItems = GetGuestCart();
            var item = guestItems.FirstOrDefault(gi => gi.ProductId == productId);
            if (item != null)
            {
                item.Quantity = Math.Min(product.Stock, item.Quantity + quantity);
            }
            else
            {
                guestItems.Add(new GuestCartItemDto
                {
                    ProductId = productId,
                    Quantity = Math.Min(product.Stock, Math.Max(1, quantity))
                });
            }
            SaveGuestCart(guestItems);
        }

        TempData["SuccessMessage"] = $"Added '{product.Title}' to your basket!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyNow(int productId, int quantity = 1)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null || !product.IsActive)
        {
            TempData["ErrorMessage"] = "Product is unavailable.";
            return RedirectToAction("Index", "Shop");
        }

        var user = await GetAuthenticatedUserAsync();
        if (user != null)
        {
            var cart = await GetOrCreateUserCartAsync(user.Id);
            if (cart != null)
            {
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
            }
            return RedirectToAction("Index", "Checkout");
        }
        else
        {
            var guestItems = GetGuestCart();
            var item = guestItems.FirstOrDefault(gi => gi.ProductId == productId);
            if (item != null)
            {
                item.Quantity = Math.Max(1, quantity);
            }
            else
            {
                guestItems.Add(new GuestCartItemDto
                {
                    ProductId = productId,
                    Quantity = Math.Max(1, quantity)
                });
            }
            SaveGuestCart(guestItems);
            return RedirectToAction("Index", "Checkout");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCartAjax(int productId, int quantity = 1)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null || !product.IsActive)
        {
            return Json(new { success = false, message = "Product is currently unavailable." });
        }

        var user = await GetAuthenticatedUserAsync();
        int totalCount = 0;

        if (user != null)
        {
            var cart = await GetOrCreateUserCartAsync(user.Id);
            if (cart != null)
            {
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
                totalCount = cart.CartItems.Sum(ci => ci.Quantity);
            }
        }
        else
        {
            var guestItems = GetGuestCart();
            var item = guestItems.FirstOrDefault(gi => gi.ProductId == productId);
            if (item != null)
            {
                item.Quantity = Math.Min(product.Stock, item.Quantity + quantity);
            }
            else
            {
                guestItems.Add(new GuestCartItemDto
                {
                    ProductId = productId,
                    Quantity = Math.Min(product.Stock, Math.Max(1, quantity))
                });
            }
            SaveGuestCart(guestItems);
            totalCount = guestItems.Sum(gi => gi.Quantity);
        }

        return Json(new { success = true, cartCount = totalCount, message = $"Added '{product.Title}' to basket!" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
    {
        var user = await GetAuthenticatedUserAsync();
        if (user != null)
        {
            var cart = await GetOrCreateUserCartAsync(user.Id);
            if (cart == null) return NotFound();

            var item = cart.CartItems.FirstOrDefault(ci => ci.Id == cartItemId || ci.ProductId == cartItemId);
            if (item == null)
            {
                return NotFound();
            }

            if (quantity <= 0)
            {
                _context.CartItems.Remove(item);
                TempData["SuccessMessage"] = "Item removed from basket.";
            }
            else
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                var maxStock = product?.Stock ?? 99;
                if (quantity > maxStock)
                {
                    TempData["ErrorMessage"] = $"Only {maxStock} units currently available in stock.";
                    item.Quantity = maxStock;
                }
                else
                {
                    item.Quantity = quantity;
                }
            }
            await _context.SaveChangesAsync();
        }
        else
        {
            var guestItems = GetGuestCart();
            var item = guestItems.FirstOrDefault(gi => gi.ProductId == cartItemId);
            if (item != null)
            {
                if (quantity <= 0)
                {
                    guestItems.Remove(item);
                    TempData["SuccessMessage"] = "Item removed from basket.";
                }
                else
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    var maxStock = product?.Stock ?? 99;
                    if (quantity > maxStock)
                    {
                        TempData["ErrorMessage"] = $"Only {maxStock} units currently available in stock.";
                        item.Quantity = maxStock;
                    }
                    else
                    {
                        item.Quantity = quantity;
                    }
                }
                SaveGuestCart(guestItems);
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int cartItemId)
    {
        var user = await GetAuthenticatedUserAsync();
        if (user != null)
        {
            var cart = await GetOrCreateUserCartAsync(user.Id);
            if (cart == null) return NotFound();

            var item = cart.CartItems.FirstOrDefault(ci => ci.Id == cartItemId || ci.ProductId == cartItemId);
            if (item == null)
            {
                return NotFound();
            }

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
        }
        else
        {
            var guestItems = GetGuestCart();
            var item = guestItems.FirstOrDefault(gi => gi.ProductId == cartItemId);
            if (item != null)
            {
                guestItems.Remove(item);
                SaveGuestCart(guestItems);
            }
        }

        TempData["SuccessMessage"] = "Item removed from basket.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveForLater(int cartItemId)
    {
        var user = await GetAuthenticatedUserAsync();
        if (user == null)
        {
            TempData["ErrorMessage"] = "Please sign in to save items to your wishlist.";
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
        }

        var cart = await GetOrCreateUserCartAsync(user.Id);
        if (cart == null) return NotFound();

        var item = cart.CartItems.FirstOrDefault(ci => ci.Id == cartItemId || ci.ProductId == cartItemId);
        if (item == null)
        {
            return NotFound();
        }

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

        _context.CartItems.Remove(item);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Item saved for later in your wishlist.";
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
