using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class OrdersController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrdersController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        return await _userManager.GetUserAsync(User);
    }

    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var orders = await _context.Orders
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderSummaryViewModel
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                TotalItems = o.OrderItems.Sum(oi => oi.Quantity)
            })
            .ToListAsync();

        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // IDOR prevention: only fetch if order belongs to authenticated user
        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

        if (order == null)
            return NotFound();

        var model = new OrderDetailViewModel
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Subtotal = order.TotalAmount - order.DeliveryFee - order.TaxAmount,
            DeliveryFee = order.DeliveryFee,
            TaxAmount = order.TaxAmount,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod ?? "Online",
            PaymentStatus = order.PaymentStatus,
            ShippingAddress = order.ShippingAddress,
            TrackingNumber = order.TrackingNumber,
            CourierName = order.CourierName,
            Items = order.OrderItems.Select(oi => new OrderItemViewModel
            {
                ProductId = oi.ProductId,
                Title = oi.Product.Title,
                ImageUrl = oi.Product.ImageUrl,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity
            }).ToList()
        };

        return View(model);
    }
}
