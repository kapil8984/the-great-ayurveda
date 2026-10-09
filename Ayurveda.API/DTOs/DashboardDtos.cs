namespace Ayurveda.API.DTOs;

public record AdminDashboardMetricsDto(
    int TotalUsers,
    int TotalDoctors,
    int ApprovedDoctors,
    int PendingDoctors,
    int TotalProducts,
    int LowStockProducts,
    int TotalOrders,
    decimal TotalRevenue,
    int TotalYogas,
    int TotalAppointments,
    List<OrderDto> RecentOrders,
    List<PendingDoctorInfoDto> PendingDoctorList
);

public record DoctorDashboardMetricsDto(
    int TotalAppointments,
    int PendingAppointments,
    int CompletedAppointments,
    int UniquePatients,
    DoctorDto Doctor,
    List<AppointmentDto> UpcomingAppointments
);
