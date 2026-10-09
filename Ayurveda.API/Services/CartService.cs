using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface ICartService
{
    Task<CartDto> GetUserCartAsync(int userId);
    Task<bool> AddToCartAsync(int userId, int productId, int quantity);
    Task<bool> UpdateQuantityAsync(int userId, int productId, int quantityChange);
    Task<bool> RemoveCartItemAsync(int userId, int productId);
    Task ClearCartAsync(int userId);
}

public class CartService : ICartService
{
    private readonly AyurvedaDbContext _context;

    public CartService(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<CartDto> GetUserCartAsync(int userId)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                    .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        var items = cart.CartItems.Select(ci => new CartItemDto(
            ci.Id,
            ci.ProductId,
            new ProductDto(
                ci.Product.Id,
                ci.Product.Title,
                ci.Product.Description,
                ci.Product.Price,
                ci.Product.Stock,
                ci.Product.Brand,
                ci.Product.ImageUrl,
                ci.Product.CategoryId,
                ci.Product.Category?.Name,
                ci.Product.AverageRating,
                0,
                ci.Product.CreatedAt
            ),
            ci.Quantity,
            ci.Quantity * ci.Product.Price
        )).ToList();

        var grandTotal = items.Sum(i => i.TotalPrice);
        return new CartDto(cart.Id, cart.UserId, items, grandTotal);
    }

    public async Task<bool> AddToCartAsync(int userId, int productId, int quantity)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return false;

        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        var existingItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity > 0 ? quantity : 1;
        }
        else
        {
            cart.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                ProductId = productId,
                Quantity = quantity > 0 ? quantity : 1
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateQuantityAsync(int userId, int productId, int quantityChange)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null) return false;

        var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
        if (item == null) return false;

        item.Quantity += quantityChange;
        if (item.Quantity <= 0)
        {
            cart.CartItems.Remove(item);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveCartItemAsync(int userId, int productId)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null) return false;

        var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
        if (item == null) return false;

        cart.CartItems.Remove(item);
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task ClearCartAsync(int userId)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart != null)
        {
            _context.CartItems.RemoveRange(cart.CartItems);
            await _context.SaveChangesAsync();
        }
    }
}
