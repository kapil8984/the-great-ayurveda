using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
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

    private async Task<Cart> GetOrCreateUserCartAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            // Fallback user for guest or demo session
            var demoUser = await _context.Users.FirstOrDefaultAsync();
            if (demoUser == null)
            {
                demoUser = new ApplicationUser { UserName = "guest@ayurveda.local", Email = "guest@ayurveda.local", FullName = "Guest User" };
                await _userManager.CreateAsync(demoUser, "GuestPass@2026");
            }
            user = demoUser;
        }

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
        var cart = await GetOrCreateUserCartAsync();

        var model = new CartViewModel
        {
            CartId = cart.Id,
            Items = cart.CartItems.Select(ci => new CartItemViewModel
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
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        var cart = await GetOrCreateUserCartAsync();
        var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);

        if (item != null)
        {
            item.Quantity += quantity;
        }
        else
        {
            cart.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = productId, Quantity = quantity });
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Formulation added to your basket!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCartAjax(int productId, int quantity = 1)
    {
        var cart = await GetOrCreateUserCartAsync();
        var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);

        if (item != null)
        {
            item.Quantity += quantity;
        }
        else
        {
            cart.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = productId, Quantity = quantity });
        }

        await _context.SaveChangesAsync();
        var totalCount = cart.CartItems.Sum(ci => ci.Quantity);
        return Json(new { success = true, cartCount = totalCount, message = "Item added to basket!" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
    {
        var item = await _context.CartItems.FindAsync(cartItemId);
        if (item != null)
        {
            if (quantity <= 0)
            {
                _context.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = quantity;
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
}
