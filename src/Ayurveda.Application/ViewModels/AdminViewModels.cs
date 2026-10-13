using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;

namespace Ayurveda.Application.ViewModels;

public class AdminDashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue => TotalOrders > 0 ? Math.Round(TotalRevenue / TotalOrders, 2) : 0m;
    public int TotalCustomers { get; set; }
    public int ActiveDoctorsCount { get; set; }
    public int ConsultationsTodayCount { get; set; }
    public int LowStockProductsCount { get; set; }
    public int PendingDoctorApprovals { get; set; }
    
    // Order Pipeline Funnel counts
    public int OrdersPlacedCount { get; set; }
    public int OrdersShippedCount { get; set; }
    public int OrdersDeliveredCount { get; set; }
    public int OrdersCancelledCount { get; set; }
    
    // Lists & summaries
    public List<AdminOrderSummaryDto> RecentOrders { get; set; } = new();
    public List<AdminPendingDoctorDto> PendingDoctors { get; set; } = new();
    public List<AdminTopProductDto> TopSellingProducts { get; set; } = new();
    public List<AdminCategoryShareDto> CategoryDistribution { get; set; } = new();
    public List<AdminLowStockProductDto> LowStockAlerts { get; set; } = new();
    public List<AuditLog> RecentAuditLogs { get; set; } = new();

    // Chart telemetry
    public List<string> SalesTrendDates { get; set; } = new();
    public List<decimal> SalesTrendAmounts { get; set; } = new();
    public int VideoConsultationsCount { get; set; }
    public int AudioConsultationsCount { get; set; }
    public int ChatConsultationsCount { get; set; }
}

public class AdminTopProductDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class AdminCategoryShareDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public double Percentage { get; set; }
}

public class AdminLowStockProductDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal Price { get; set; }
}

public class AdminOrderSummaryDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
}

public class AdminPendingDoctorDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public string Qualification { get; set; } = string.Empty;
    public string? QualificationImage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminDoctorApprovalActionDto
{
    public int DoctorId { get; set; }
    public bool Approve { get; set; }
    public string? RejectionReason { get; set; }
}

public class AdminDoctorListViewModel
{
    public List<Doctor> Doctors { get; set; } = new();
    public string? SelectedStatus { get; set; }
    public string? SelectedSpecialization { get; set; }
    public string? SearchQuery { get; set; }
    public int TotalCount { get; set; }
}

public class AdminDoctorDetailsViewModel
{
    public Doctor Doctor { get; set; } = null!;
    public List<Appointment> RecentAppointments { get; set; } = new();
    public List<DoctorReview> Reviews { get; set; } = new();
    public decimal GrossEarnings { get; set; }
    public decimal SettledPayouts { get; set; }
}

public class AdminUserListViewModel
{
    public List<ApplicationUser> Users { get; set; } = new();
    public Dictionary<int, string> UserRoles { get; set; } = new();
    public string? SelectedRole { get; set; }
    public string? SearchQuery { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalUsers { get; set; }
}

public class AdminUserDetailsViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public List<Order> Orders { get; set; } = new();
    public List<Appointment> Consultations { get; set; } = new();
    public List<Address> Addresses { get; set; } = new();
}

public class AdminOrderListViewModel
{
    public List<Order> Orders { get; set; } = new();
    public string? SelectedStatus { get; set; }
    public string? SearchQuery { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class AdminProductListViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public int? SelectedCategoryId { get; set; }
    public bool? LowStockOnly { get; set; }
    public bool? OutOfStockOnly { get; set; }
    public string? SearchQuery { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class AdminProductEditViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Mrp { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public string Brand { get; set; } = "The Great Ayurveda Labs";
    public string Description { get; set; } = string.Empty;
    public string? KeyIngredients { get; set; }
    public string? HowToUse { get; set; }
    public string ImageUrl { get; set; } = "/images/products/placeholder.svg";
    public bool IsActive { get; set; } = true;
    public bool IsBestseller { get; set; }
    public bool IsFeatured { get; set; }
    public List<string> GalleryImageUrls { get; set; } = new();
}

public class AdminFinanceOverviewViewModel
{
    public decimal GrossECommerceRevenue { get; set; }
    public decimal GrossConsultationRevenue { get; set; }
    public decimal TotalGrossRevenue => GrossECommerceRevenue + GrossConsultationRevenue;
    public decimal PlatformConsultationFeeCommission { get; set; }
    public decimal TotalGstCollected { get; set; }
    public decimal PendingDoctorPayouts { get; set; }
    public decimal CompletedDoctorPayouts { get; set; }
    public List<Order> RecentTransactions { get; set; } = new();
    public List<DoctorPayoutItemDto> DoctorPayoutLedger { get; set; } = new();
}

public class DoctorPayoutItemDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int CompletedConsultations { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal CommissionDeducted { get; set; }
    public decimal NetPayable { get; set; }
    public decimal Withdrawn { get; set; }
    public decimal CurrentBalance => NetPayable - Withdrawn;
}

public class AdminSystemSettingsViewModel
{
    public decimal FreeShippingThreshold { get; set; } = 799m;
    public decimal StandardShippingFee { get; set; } = 70m;
    public decimal GstPercentageRate { get; set; } = 12m;
    public decimal DoctorCommissionPercentageRate { get; set; } = 15m;
    public int ConsultationSlotDurationMinutes { get; set; } = 30;
    public bool EnableIpAllowlisting { get; set; }
    public string? AllowedIpListCsv { get; set; }
}

public class AdminSystemHealthViewModel
{
    public bool DatabaseHealthy { get; set; }
    public string DatabaseProvider { get; set; } = string.Empty;
    public int DatabaseItemCount { get; set; }
    public bool EmailServiceConfigured { get; set; }
    public bool FileStorageHealthy { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string MemoryUsageMb { get; set; } = string.Empty;
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
}

public class AdminUserEditViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
}

public class AdminDoctorEditViewModel
{
    public int Id { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public string Qualification { get; set; } = "BAMS";
    public int Experience { get; set; }
    public decimal ConsultationFee { get; set; } = 499m;
    public string? ClinicAddress { get; set; }
    public string? Bio { get; set; }
    public string? ProfileImage { get; set; }
    public bool IsOnline { get; set; } = true;
    public int Status { get; set; } = 1;
}
