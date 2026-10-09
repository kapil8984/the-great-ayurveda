using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(int userId, CreateOrderRequest request);
    Task<List<OrderDto>> GetUserOrdersAsync(int userId);
    Task<List<OrderDto>> GetAllOrdersAsync();
    Task<OrderDto?> GetOrderByIdAsync(int orderId);
    Task<bool> UpdateOrderStatusAsync(int orderId, string status);
}

public class OrderService : IOrderService
{
    private readonly AyurvedaDbContext _context;
    private readonly ICartService _cartService;

    public OrderService(AyurvedaDbContext context, ICartService cartService)
    {
        _context = context;
        _cartService = cartService;
    }

    public async Task<OrderDto> CreateOrderAsync(int userId, CreateOrderRequest request)
    {
        // Execute inside an explicit SQL database transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            // Determine order items either from request body or user's active cart
            var requestedItems = new List<(int ProductId, int Quantity)>();

            if (request.Products != null && request.Products.Count > 0)
            {
                foreach (var item in request.Products)
                {
                    int prodId = 0;
                    if (item.ProductId.HasValue && item.ProductId.Value > 0)
                    {
                        prodId = item.ProductId.Value;
                    }
                    else if (item.Product != null)
                    {
                        if (item.Product is System.Text.Json.JsonElement elem)
                        {
                            if (elem.ValueKind == System.Text.Json.JsonValueKind.Number)
                                prodId = elem.GetInt32();
                            else if (elem.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(elem.GetString(), out int parsed))
                                prodId = parsed;
                            else if (elem.ValueKind == System.Text.Json.JsonValueKind.Object && elem.TryGetProperty("id", out var idProp))
                                prodId = idProp.GetInt32();
                        }
                        else if (int.TryParse(item.Product.ToString(), out int parsedId))
                        {
                            prodId = parsedId;
                        }
                    }

                    if (prodId > 0 && item.Quantity > 0)
                    {
                        requestedItems.Add((prodId, item.Quantity));
                    }
                }
            }

            // If no products sent in body, load from user's cart
            if (requestedItems.Count == 0)
            {
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart != null && cart.CartItems.Count > 0)
                {
                    requestedItems.AddRange(cart.CartItems.Select(ci => (ci.ProductId, ci.Quantity)));
                }
            }

            if (requestedItems.Count == 0)
            {
                throw new InvalidOperationException("No items available to order.");
            }

            decimal totalAmount = 0m;
            var orderItems = new List<OrderItem>();

            foreach (var (productId, quantity) in requestedItems)
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    throw new InvalidOperationException($"Product with ID {productId} not found.");
                }

                if (product.Stock < quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for '{product.Title}'. Available: {product.Stock}, requested: {quantity}.");
                }

                // Decrement stock concurrently safe within transaction
                product.Stock -= quantity;

                // Save historical unit price
                var unitPrice = product.Price;
                totalAmount += unitPrice * quantity;

                orderItems.Add(new OrderItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    ReviewStatus = 0
                });
            }

            var order = new Order
            {
                UserId = userId,
                TotalAmount = totalAmount,
                OrderDate = DateTime.UtcNow,
                Status = "Placed",
                ShippingAddress = request.ShippingAddress ?? user.Contact,
                PaymentMethod = request.PaymentMethod ?? "CashOnDelivery",
                OrderItems = orderItems
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Clear user's cart now that order is placed
            await _cartService.ClearCartAsync(userId);

            await transaction.CommitAsync();

            return await GetOrderByIdAsync(order.Id) ?? throw new InvalidOperationException("Failed to load placed order.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Product inventory was modified by another order during checkout. Please review your cart and try again.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<OrderDto>> GetUserOrdersAsync(int userId)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Category)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return orders.Select(MapToDto).ToList();
    }

    public async Task<List<OrderDto>> GetAllOrdersAsync()
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Category)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        return order == null ? null : MapToDto(order);
    }

    public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return false;

        order.Status = status;
        await _context.SaveChangesAsync();
        return true;
    }

    private static OrderDto MapToDto(Order o) => new(
        o.Id,
        o.UserId,
        o.TotalAmount,
        o.OrderDate,
        o.Status,
        o.ShippingAddress,
        o.PaymentMethod,
        o.OrderItems.Select(oi => new OrderItemDto(
            oi.Id,
            oi.ProductId,
            oi.Product != null ? new ProductDto(
                oi.Product.Id,
                oi.Product.Title,
                oi.Product.Description,
                oi.Product.Price,
                oi.Product.Stock,
                oi.Product.Brand,
                oi.Product.ImageUrl,
                oi.Product.CategoryId,
                oi.Product.Category?.Name,
                oi.Product.AverageRating,
                0,
                oi.Product.CreatedAt
            ) : null,
            oi.Quantity,
            oi.UnitPrice,
            oi.UnitPrice * oi.Quantity,
            oi.ReviewStatus
        )).ToList()
    );
}
