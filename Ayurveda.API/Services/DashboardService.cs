using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface IDashboardService
{
    Task<AdminDashboardMetricsDto> GetAdminMetricsAsync();
    Task<DoctorDashboardMetricsDto?> GetDoctorMetricsAsync(int doctorId);
}

public class DashboardService : IDashboardService
{
    private readonly AyurvedaDbContext _context;
    private readonly IDoctorService _doctorService;
    private readonly IOrderService _orderService;
    private readonly IAppointmentService _appointmentService;

    public DashboardService(
        AyurvedaDbContext context,
        IDoctorService doctorService,
        IOrderService orderService,
        IAppointmentService appointmentService)
    {
        _context = context;
        _doctorService = doctorService;
        _orderService = orderService;
        _appointmentService = appointmentService;
    }

    public async Task<AdminDashboardMetricsDto> GetAdminMetricsAsync()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalDoctors = await _context.Doctors.CountAsync();
        var approvedDoctors = await _context.DoctorDetails.CountAsync(d => d.Status == 1);
        var pendingDoctors = await _context.DoctorDetails.CountAsync(d => d.Status == 0);
        var totalProducts = await _context.Products.CountAsync();
        var lowStockProducts = await _context.Products.CountAsync(p => p.Stock < 20);
        var totalOrders = await _context.Orders.CountAsync();
        var totalRevenue = await _context.Orders.SumAsync(o => o.TotalAmount);
        var totalYogas = await _context.Yogas.CountAsync();
        var totalAppointments = await _context.Appointments.CountAsync();

        var recentOrders = (await _orderService.GetAllOrdersAsync()).Take(5).ToList();
        var pendingDoctorList = await _doctorService.GetPendingDoctorsAsync();

        return new AdminDashboardMetricsDto(
            totalUsers,
            totalDoctors,
            approvedDoctors,
            pendingDoctors,
            totalProducts,
            lowStockProducts,
            totalOrders,
            totalRevenue,
            totalYogas,
            totalAppointments,
            recentOrders,
            pendingDoctorList
        );
    }

    public async Task<DoctorDashboardMetricsDto?> GetDoctorMetricsAsync(int doctorId)
    {
        var doctor = await _doctorService.GetDoctorByIdAsync(doctorId);
        if (doctor == null) return null;

        var totalAppointments = await _context.Appointments.CountAsync(a => a.DoctorId == doctorId);
        var pendingAppointments = await _context.Appointments.CountAsync(a => a.DoctorId == doctorId && a.Status == "Pending");
        var completedAppointments = await _context.Appointments.CountAsync(a => a.DoctorId == doctorId && a.Status == "Completed");
        var uniquePatients = await _context.Appointments.Where(a => a.DoctorId == doctorId).Select(a => a.UserId).Distinct().CountAsync();

        var upcomingAppointments = (await _appointmentService.GetDoctorAppointmentsAsync(doctorId)).Take(10).ToList();

        return new DoctorDashboardMetricsDto(
            totalAppointments,
            pendingAppointments,
            completedAppointments,
            uniquePatients,
            doctor,
            upcomingAppointments
        );
    }
}
