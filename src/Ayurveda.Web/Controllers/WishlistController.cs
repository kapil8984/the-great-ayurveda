using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class WishlistController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public WishlistController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }
        return await _userManager.GetUserAsync(User);
    }

    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Wishlist") });
        }

        var items = await _context.WishlistItems
            .Include(w => w.Product)
            .Where(w => w.UserId == user.Id && w.Product.IsActive)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WishlistItemViewModel
            {
                Id = w.Id,
                ProductId = w.ProductId,
                Title = w.Product.Title,
                Brand = w.Product.Brand,
                Price = w.Product.Price,
                Mrp = w.Product.Mrp,
                DiscountPercent = w.Product.DiscountPercent,
                ImageUrl = w.Product.ImageUrl,
                IsInStock = w.Product.Stock > 0,
                AverageRating = w.Product.AverageRating,
                AddedAt = w.CreatedAt
            })
            .ToListAsync();

        var model = new WishlistViewModel { Items = items };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int productId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Json(new { success = false, message = "Please sign in to save items to your wishlist.", redirect = "/Account/Login" });
        }

        var existing = await _context.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == productId);

        bool inWishlist;
        string msg;

        if (existing != null)
        {
            _context.WishlistItems.Remove(existing);
            inWishlist = false;
            msg = "Removed from your wishlist.";
        }
        else
        {
            _context.WishlistItems.Add(new WishlistItem
            {
                UserId = user.Id,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            });
            inWishlist = true;
            msg = "Added to your wishlist!";
        }

        await _context.SaveChangesAsync();
        var totalCount = await _context.WishlistItems.CountAsync(w => w.UserId == user.Id);

        return Json(new { success = true, inWishlist, totalCount, message = msg });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId)
    {
        var user = await GetCurrentUserAsync();
        if (user != null)
        {
            var item = await _context.WishlistItems.FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == productId);
            if (item != null)
            {
                _context.WishlistItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Item removed from wishlist.";
            }
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToCart(int productId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Wishlist") });
        }

        // 1. Remove from Wishlist
        var wishItem = await _context.WishlistItems.FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == productId);
        if (wishItem != null)
        {
            _context.WishlistItems.Remove(wishItem);
        }

        // 2. Add to Cart
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (cart == null)
        {
            cart = new Cart { UserId = user.Id };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
        if (cartItem != null)
        {
            cartItem.Quantity += 1;
        }
        else
        {
            cart.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = productId, Quantity = 1 });
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Moved to your basket!";
        return RedirectToAction("Index", "Cart");
    }
}
