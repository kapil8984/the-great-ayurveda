using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class OrdersController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public OrdersController(
        AyurvedaDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null && !string.IsNullOrEmpty(User.Identity.Name))
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name || u.Email == User.Identity.Name);
        }
        return user;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null && User.Identity?.IsAuthenticated == true)
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name || u.Email == User.Identity.Name);
        }

        if (user != null && (User.IsInRole("Doctor") || await _userManager.IsInRoleAsync(user, "Doctor")))
        {
            return RedirectToAction("Dashboard", "DoctorPortal");
        }

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
                TotalItems = o.OrderItems.Sum(oi => oi.Quantity),
                FirstItemTitle = o.OrderItems.OrderBy(oi => oi.Id).Select(oi => oi.Product.Title).FirstOrDefault(),
                FirstItemImage = o.OrderItems.OrderBy(oi => oi.Id).Select(oi => oi.Product.ImageUrl).FirstOrDefault()
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
            .FirstOrDefaultAsync(o => o.Id == id && (o.UserId == user.Id || User.IsInRole("Admin") || User.IsInRole("Doctor")));

        if (order == null)
            return NotFound();

        // Build 5-stage timeline
        var stages = new[]
        {
            ("Placed", "Order verified and sent to pharmacy dispatch.", order.OrderDate),
            ("Packed", "Items inspected and sealed in tamper-evident pack.", order.OrderDate.AddHours(4)),
            ("Shipped", $"Handed over to {order.CourierName ?? "BlueDart Express Healthcare"}.", order.OrderDate.AddHours(12)),
            ("OutForDelivery", "Courier agent is on the way to your delivery address.", order.OrderDate.AddDays(2)),
            ("Delivered", "Package safely received.", order.DeliveryDate ?? order.OrderDate.AddDays(3))
        };

        var stageNames = stages.Select(s => s.Item1).ToList();
        var currentStageIndex = stageNames.IndexOf(order.Status);
        if (currentStageIndex == -1) currentStageIndex = 0;

        var timeline = new List<OrderTimelineStep>();
        for (int i = 0; i < stages.Length; i++)
        {
            var isPassed = i <= currentStageIndex;
            var isCurrent = i == currentStageIndex;
            timeline.Add(new OrderTimelineStep
            {
                Title = stages[i].Item1,
                Description = stages[i].Item2,
                IsCompleted = isPassed,
                IsCurrent = isCurrent,
                Timestamp = isPassed ? stages[i].Item3 : null
            });
        }

        var model = new OrderDetailViewModel
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Subtotal = order.TotalAmount - order.DeliveryFee - order.TaxAmount + order.DiscountAmount,
            DiscountAmount = order.DiscountAmount,
            DeliveryFee = order.DeliveryFee,
            TaxAmount = order.TaxAmount,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod ?? "Online",
            PaymentStatus = order.PaymentStatus,
            ShippingAddress = order.ShippingAddress,
            TrackingNumber = order.TrackingNumber,
            CourierName = order.CourierName,
            DeliveryDate = order.DeliveryDate,
            DeliveryType = order.DeliveryType,
            CancellationReason = order.CancellationReason,
            ReturnReason = order.ReturnReason,
            ReturnStatus = order.ReturnStatus,
            Timeline = timeline,
            Items = order.OrderItems.Select(oi => new OrderItemViewModel
            {
                ProductId = oi.ProductId,
                Title = oi.Product.Title,
                ImageUrl = oi.Product.ImageUrl,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity
            }).ToList()
        };

        if (double.TryParse(_configuration["Demo:WarehouseLat"] ?? _configuration["Demo__WarehouseLat"], out var whLat) && whLat != 0)
        {
            model.WarehouseLat = whLat;
        }
        if (double.TryParse(_configuration["Demo:WarehouseLng"] ?? _configuration["Demo__WarehouseLng"], out var whLng) && whLng != 0)
        {
            model.WarehouseLng = whLng;
        }

        return View(model);
    }

    [HttpGet]
    public Task<IActionResult> Track(int id) => Details(id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id, string? reason)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

        if (order == null) return NotFound();

        if (order.Status != OrderStatus.Placed.ToString() && order.Status != OrderStatus.Packed.ToString())
        {
            TempData["ErrorMessage"] = "Order can only be cancelled while in 'Placed' or 'Packed' status.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Restore stock
        foreach (var item in order.OrderItems)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.Stock += item.Quantity;
            }
        }

        order.Status = OrderStatus.Cancelled.ToString();
        order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Customer requested cancellation." : reason.Trim();

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Order Cancelled",
            Message = $"Order #{order.OrderNumber} has been cancelled.",
            Type = "Order",
            TargetUrl = $"/Orders/Details/{order.Id}"
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Order cancelled successfully. If paid online, your refund will be processed within 3-5 days.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReturn(int id, string? reason)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
        if (order == null) return NotFound();

        if (order.Status != OrderStatus.Delivered.ToString())
        {
            TempData["ErrorMessage"] = "Only delivered orders are eligible for return.";
            return RedirectToAction(nameof(Details), new { id });
        }

        order.ReturnStatus = "Requested";
        order.ReturnReason = string.IsNullOrWhiteSpace(reason) ? "Defective or damaged seal upon delivery." : reason.Trim();

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Return Request Received",
            Message = $"Return request for Order #{order.OrderNumber} has been logged.",
            Type = "Order",
            TargetUrl = $"/Orders/Details/{order.Id}"
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Return request initiated. A logistics representative will inspect the item within 48 hours.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

        if (order == null) return NotFound();

        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (cart == null)
        {
            cart = new Cart { UserId = user.Id };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        foreach (var item in order.OrderItems)
        {
            var existing = cart.CartItems.FirstOrDefault(ci => ci.ProductId == item.ProductId);
            if (existing != null)
            {
                existing.Quantity += item.Quantity;
            }
            else
            {
                cart.CartItems.Add(new CartItem
                {
                    CartId = cart.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "All items from this order have been added to your basket!";
        return RedirectToAction("Index", "Cart");
    }

    [HttpGet]
    public async Task<IActionResult> Invoice(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

        if (order == null) return NotFound();

        var model = new OrderDetailViewModel
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Subtotal = order.TotalAmount - order.DeliveryFee - order.TaxAmount + order.DiscountAmount,
            DiscountAmount = order.DiscountAmount,
            DeliveryFee = order.DeliveryFee,
            TaxAmount = order.TaxAmount,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod ?? "Online",
            PaymentStatus = order.PaymentStatus,
            ShippingAddress = order.ShippingAddress,
            TrackingNumber = order.TrackingNumber,
            CourierName = order.CourierName,
            DeliveryType = order.DeliveryType,
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

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
        if (order != null && (order.Status == "Placed" || order.Status == "Confirmed"))
        {
            order.Status = "Cancelled";
            order.CancellationReason = "Cancelled by patient before dispatch";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Order #{order.OrderNumber} has been cancelled successfully.";
        }
        else if (order != null)
        {
            TempData["ErrorMessage"] = $"Order #{order.OrderNumber} cannot be cancelled as it is already {order.Status}.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
