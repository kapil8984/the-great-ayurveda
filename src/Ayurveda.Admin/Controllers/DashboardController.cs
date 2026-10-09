using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
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
        var totalRev = await _context.Orders.Where(o => o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var totalOrders = await _context.Orders.CountAsync();
        var totalCustomers = await _context.Users.CountAsync();
        var pendingDoctorsCount = await _context.Doctors.CountAsync(d => d.DoctorDetail != null && d.DoctorDetail.Status == 0);

        var recentOrders = await _context.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
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
                Specialization = d.DoctorDetail != null ? d.DoctorDetail.Specialization.Name : "General",
                ExperienceYears = d.DoctorDetail != null ? d.DoctorDetail.Experience : 0,
                Qualification = d.DoctorDetail != null ? d.DoctorDetail.Qualification ?? "BAMS" : "BAMS",
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalRevenue = totalRev,
            TotalOrders = totalOrders,
            TotalCustomers = totalCustomers,
            PendingDoctorApprovals = pendingDoctorsCount,
            RecentOrders = recentOrders,
            PendingDoctors = pendingDocs
        };

        return View(model);
    }
}
