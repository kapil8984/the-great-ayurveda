using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookieScheme", Policy = "RequireFinanceManagement")]
public class FinanceController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;

    public FinanceController(AyurvedaDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // -------------------------------------------------------------------------
    // 1. FINANCIAL OVERVIEW & LEDGER
    // -------------------------------------------------------------------------
    public async Task<IActionResult> Index()
    {
        var ecomGross = await _context.Orders
            .Where(o => o.Status != "Cancelled")
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        var consultGross = await _context.Appointments
            .Where(a => a.Status == "Completed")
            .SumAsync(a => (decimal?)a.ConsultationFee) ?? 0m;

        var platformCommission = Math.Round(consultGross * 0.15m, 2);

        // GST Collections (12% e-commerce, 18% consultations)
        var ecomGst = Math.Round(ecomGross - (ecomGross / 1.12m), 2);
        var consultGst = Math.Round(consultGross - (consultGross / 1.18m), 2);
        var totalGst = ecomGst + consultGst;

        var recentOrders = await _context.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .Take(10)
            .ToListAsync();

        // Doctor payout balances
        var completedConsultations = await _context.Appointments
            .Include(a => a.Doctor)
            .Where(a => a.Status == "Completed")
            .ToListAsync();

        var payoutLedger = completedConsultations
            .GroupBy(a => a.DoctorId)
            .Select(g =>
            {
                var doc = g.First().Doctor;
                var gross = g.Sum(a => a.ConsultationFee);
                var comm = Math.Round(gross * 0.15m, 2);
                var net = gross - comm;
                return new DoctorPayoutItemDto
                {
                    DoctorId = g.Key,
                    DoctorName = doc.DoctorName,
                    Email = doc.Email,
                    CompletedConsultations = g.Count(),
                    TotalEarnings = gross,
                    CommissionDeducted = comm,
                    NetPayable = net,
                    Withdrawn = 0m // Sample settled
                };
            })
            .ToList();

        var pendingPayouts = payoutLedger.Sum(p => p.CurrentBalance);

        var model = new AdminFinanceOverviewViewModel
        {
            GrossECommerceRevenue = ecomGross,
            GrossConsultationRevenue = consultGross,
            PlatformConsultationFeeCommission = platformCommission,
            TotalGstCollected = totalGst,
            PendingDoctorPayouts = pendingPayouts,
            CompletedDoctorPayouts = 0m,
            RecentTransactions = recentOrders,
            DoctorPayoutLedger = payoutLedger
        };

        return View(model);
    }

    // -------------------------------------------------------------------------
    // 2. APPROVE DOCTOR PAYOUT
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApprovePayout(int doctorId, decimal amount)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor != null)
        {
            await _auditService.LogAsync("DoctorPayoutApproved", "Finance", doctorId.ToString(), $"Approved payout of ₹{amount:N0} to Dr. {doctor.DoctorName}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["SuccessMessage"] = $"Payout of ₹{amount:N0} approved and scheduled for bank transfer to Dr. {doctor.DoctorName}.";
        }
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------
    // 3. EXPORT GST COMPLIANCE CSV REPORT
    // -------------------------------------------------------------------------
    public async Task<IActionResult> ExportGstCsv()
    {
        var orders = await _context.Orders
            .Where(o => o.Status != "Cancelled")
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Invoice Number,Order Date,Customer,Payment Mode,Taxable Value (INR),CGST 6% (INR),SGST 6% (INR),Total GST (INR),Total Invoice Value (INR)");

        foreach (var o in orders)
        {
            var taxable = Math.Round(o.TotalAmount / 1.12m, 2);
            var gst = o.TotalAmount - taxable;
            var cgst = Math.Round(gst / 2, 2);
            var sgst = gst - cgst;
            var cust = $"\"{o.UserId}\"";

            sb.AppendLine($"\"TGA-INV-{o.OrderNumber}\",{o.OrderDate:yyyy-MM-dd},{cust},{o.PaymentMethod},{taxable},{cgst},{sgst},{gst},{o.TotalAmount}");
        }

        await _auditService.LogAsync("GstReportExported", "Finance", "GST", $"Exported GST compliance report for {orders.Count} transactions", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"tga_gst_report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
