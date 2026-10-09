using System.Security.Claims;
using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return (claim != null && int.TryParse(claim.Value, out int id)) ? id : 0;
    }

    [HttpPost("order/add-order")]
    [HttpPost("api/orders")]
    public async Task<IActionResult> AddOrder([FromBody] CreateOrderRequest request)
    {
        // IDOR KILL: Ignore any request.UserId in body, strictly derive from JWT identity (Rule c)
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { message = "Authentication required to place an order." });

        try
        {
            var order = await _orderService.CreateOrderAsync(userId, request);
            return StatusCode(201, new { message = "Order created successfully", order });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, error = ex.Message });
        }
    }

    [HttpPost("order/get-order-details")]
    public async Task<IActionResult> GetUserOrdersPost()
    {
        // IDOR KILL: Ignore request body UserId, derive strictly from JWT
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { error = "Authentication required." });

        var orders = await _orderService.GetUserOrdersAsync(userId);

        // Map to legacy frontend structure:
        var legacyOrders = orders.Select(o => new
        {
            _id = o.Id,
            id = o.Id,
            totalAmount = o.TotalAmount,
            orderDate = o.OrderDate,
            status = o.Status,
            shippingAddress = o.ShippingAddress,
            products = o.Products.Select(p => new
            {
                product = p.Product != null ? new
                {
                    _id = p.Product.Id,
                    id = p.Product.Id,
                    title = p.Product.Title,
                    price = p.UnitPrice,
                    imageURL = p.Product.ImageUrl,
                    description = p.Product.Description
                } : null,
                quantity = p.Quantity,
                review = p.Review
            }).ToList()
        }).ToList();

        return Ok(new { success = true, orders = legacyOrders });
    }

    [HttpGet("api/orders")]
    public async Task<IActionResult> GetOrders()
    {
        int userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        var orders = isAdmin
            ? await _orderService.GetAllOrdersAsync()
            : await _orderService.GetUserOrdersAsync(userId);

        return Ok(orders);
    }

    [HttpGet("api/orders/{id}")]
    public async Task<IActionResult> GetOrderById(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound(new { message = "Order not found" });

        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        // Authorization check: user can only view their own order
        if (!isAdmin && order.UserId != currentUserId)
        {
            return Forbid();
        }

        return Ok(order);
    }

    // Rule a: Order status update -> Admin only!
    [HttpPut("api/orders/{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var success = await _orderService.UpdateOrderStatusAsync(id, request.Status);
        if (!success) return NotFound(new { message = "Order not found" });
        return Ok(new { message = "Order status updated successfully" });
    }
}
