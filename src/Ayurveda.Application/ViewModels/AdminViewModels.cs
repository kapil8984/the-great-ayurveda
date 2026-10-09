namespace Ayurveda.Application.ViewModels;

public class AdminDashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalDoctors { get; set; }
    public int PendingDoctorApprovals { get; set; }
    public int LowStockProductsCount { get; set; }
    public List<AdminOrderSummaryDto> RecentOrders { get; set; } = new();
    public List<AdminPendingDoctorDto> PendingDoctors { get; set; } = new();
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
