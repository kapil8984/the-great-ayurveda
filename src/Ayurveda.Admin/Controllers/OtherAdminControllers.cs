using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Roles = "SuperAdmin,CatalogManager,Admin")]
public class ProductsController : Controller
{
    private readonly AyurvedaDbContext _context;

    public ProductsController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        return View(products);
    }
}

[Authorize(Roles = "SuperAdmin,SupportAgent,FinanceManager,Admin")]
public class OrdersController : Controller
{
    private readonly AyurvedaDbContext _context;

    public OrdersController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _context.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, string status)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.Status = status;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Order #{order.OrderNumber} status updated to {status}.";
        }
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = "SuperAdmin,CatalogManager,Admin")]
public class YogaController : Controller
{
    private readonly AyurvedaDbContext _context;

    public YogaController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var yogas = await _context.Yogas
            .Include(y => y.Category)
            .OrderBy(y => y.YogaName)
            .ToListAsync();

        return View(yogas);
    }
}

[Authorize(Roles = "SuperAdmin,SupportAgent,Admin")]
public class UsersController : Controller
{
    private readonly AyurvedaDbContext _context;

    public UsersController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return View(users);
    }
}
