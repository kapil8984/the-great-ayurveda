using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Roles = "SuperAdmin,SupportAgent,FinanceManager")]
public class OrdersController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;

    public OrdersController(AyurvedaDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // -------------------------------------------------------------------------
    // 1. ORDER LIST & STATUS FILTER
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Index(string? status, string? search, int page = 1)
    {
        int pageSize = 15;
        var q = _context.Orders
            .Include(o => o.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            q = q.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(search) || (o.User != null && o.User.Email != null && o.User.Email.Contains(search)));
        }

        var total = await q.CountAsync();
        var orders = await q
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var model = new AdminOrderListViewModel
        {
            Orders = orders,
            SelectedStatus = status,
            SearchQuery = search,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            TotalCount = total
        };

        return View(model);
    }

    // -------------------------------------------------------------------------
    // 2. ORDER DETAILS & TIMELINE
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        return View(order);
    }

    // -------------------------------------------------------------------------
    // 3. STATUS TRANSITIONS & TRACKING NUMBER
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, string status, string? trackingNumber, string? notes)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        var oldStatus = order.Status;
        order.Status = status;

        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            order.TrackingNumber = trackingNumber.Trim();
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("OrderStatusUpdated", "Order", orderId.ToString(), $"Updated order #{order.OrderNumber} status from '{oldStatus}' to '{status}'. Tracking: {trackingNumber ?? "N/A"}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Order #{order.OrderNumber} status updated to {status}.";
        return RedirectToAction(nameof(Details), new { id = orderId });
    }

    // -------------------------------------------------------------------------
    // 4. REFUND & RETURN PROCESSING (WITH COD RECONCILIATION)
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,FinanceManager")]
    public async Task<IActionResult> ProcessRefund(int orderId, decimal amount, string reason)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        order.Status = "Cancelled";
        await _context.SaveChangesAsync();

        // Record refund payment entry
        var refundPayment = new Payment
        {
            OrderId = order.Id,
            Provider = "Razorpay",
            TransactionId = $"REFUND-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            Amount = amount,
            Status = Domain.Enums.PaymentStatus.Refunded,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.Add(refundPayment);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("OrderRefundProcessed", "Order", orderId.ToString(), $"Processed refund of ₹{amount} for order #{order.OrderNumber}. Reason: {reason}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["SuccessMessage"] = $"Refund of ₹{amount:N0} processed for Order #{order.OrderNumber}.";
        return RedirectToAction(nameof(Details), new { id = orderId });
    }

    // -------------------------------------------------------------------------
    // 5. PACKING SLIP & TAX INVOICE
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Invoice(int id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        return View(order);
    }
}
