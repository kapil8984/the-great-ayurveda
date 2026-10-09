using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Roles = "SuperAdmin,CatalogManager,SupportAgent,FinanceManager,Admin")]
public class DashboardController : Controller
{
    private readonly AyurvedaDbContext _context;

    public DashboardController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;

        // Core KPIs
        var totalRev = await _context.Orders
            .Where(o => o.Status != "Cancelled")
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        var totalOrders = await _context.Orders.CountAsync();
        var totalCustomers = await _context.Users.CountAsync();
        var activeDoctorsCount = await _context.Doctors.CountAsync(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1);
        var pendingDoctorsCount = await _context.Doctors.CountAsync(d => d.DoctorDetail != null && d.DoctorDetail.Status == 0);
        var lowStockCount = await _context.Products.CountAsync(p => p.Stock < 10 && p.IsActive);
        var consultationsToday = await _context.Appointments.CountAsync(a => a.AppointmentDate.Date == today);

        // Recent Orders
        var recentOrders = await _context.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .Take(6)
            .Select(o => new AdminOrderSummaryDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerEmail = o.User != null && o.User.Email != null ? o.User.Email : "Customer",
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                OrderDate = o.OrderDate
            })
            .ToListAsync();

        // Pending Doctors Queue
        var pendingDocs = await _context.Doctors
            .Include(d => d.DoctorDetail)
                .ThenInclude(dd => dd!.Specialization)
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 0)
            .Take(5)
            .Select(d => new AdminPendingDoctorDto
            {
                DoctorId = d.Id,
                DoctorName = d.DoctorName,
                Email = d.Email,
                ContactNumber = d.ContactNumber,
                Specialization = d.DoctorDetail != null && d.DoctorDetail.Specialization != null ? d.DoctorDetail.Specialization.Name : "General",
                ExperienceYears = d.DoctorDetail != null ? d.DoctorDetail.Experience : 0,
                Qualification = d.DoctorDetail != null ? d.DoctorDetail.Qualification ?? "BAMS" : "BAMS",
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        // Low Stock Alerts
        var lowStockProducts = await _context.Products
            .Where(p => p.Stock < 10 && p.IsActive)
            .OrderBy(p => p.Stock)
            .Take(5)
            .Select(p => new AdminLowStockProductDto
            {
                Id = p.Id,
                Title = p.Title,
                Sku = p.Sku ?? "TGA",
                StockQuantity = p.Stock,
                Price = p.Price
            })
            .ToListAsync();

        // Top Selling Formulations
        var topProducts = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Reviews.Count)
            .Take(5)
            .Select(p => new AdminTopProductDto
            {
                Id = p.Id,
                Title = p.Title,
                Category = p.Category != null ? p.Category.Name : "Ayurveda",
                Price = p.Price,
                Stock = p.Stock
            })
            .ToListAsync();

        // Category Share
        var categories = await _context.Categories
            .Include(c => c.Products)
            .Where(c => c.IsActive)
            .Take(6)
            .ToListAsync();

        var totalCatalogItems = Math.Max(1, await _context.Products.CountAsync());
        var categoryShares = categories.Select(c => new AdminCategoryShareDto
        {
            CategoryName = c.Name,
            ProductCount = c.Products.Count,
            Percentage = Math.Round((double)c.Products.Count / totalCatalogItems * 100, 1)
        }).ToList();

        // Telemedicine volume by mode
        var videoCount = await _context.Appointments.CountAsync(a => a.ConsultationType == ConsultationType.Video);
        var audioCount = await _context.Appointments.CountAsync(a => a.ConsultationType == ConsultationType.Audio);
        var chatCount = await _context.Appointments.CountAsync(a => a.ConsultationType == ConsultationType.Chat);

        // Sales trend (Last 7 days)
        var trendDates = new List<string>();
        var trendAmounts = new List<decimal>();
        for (int i = 6; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            trendDates.Add(day.ToString("dd MMM"));
            var daySales = await _context.Orders
                .Where(o => o.OrderDate.Date == day && o.Status != "Cancelled")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
            trendAmounts.Add(daySales);
        }

        // Recent Audit logs
        var recentAuditLogs = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(6)
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalRevenue = totalRev,
            TotalOrders = totalOrders,
            TotalCustomers = totalCustomers,
            ActiveDoctorsCount = activeDoctorsCount,
            PendingDoctorApprovals = pendingDoctorsCount,
            ConsultationsTodayCount = consultationsToday,
            LowStockProductsCount = lowStockCount,
            RecentOrders = recentOrders,
            PendingDoctors = pendingDocs,
            LowStockAlerts = lowStockProducts,
            TopSellingProducts = topProducts,
            CategoryDistribution = categoryShares,
            SalesTrendDates = trendDates,
            SalesTrendAmounts = trendAmounts,
            VideoConsultationsCount = videoCount,
            AudioConsultationsCount = audioCount,
            ChatConsultationsCount = chatCount,
            RecentAuditLogs = recentAuditLogs
        };

        return View(model);
    }
}
