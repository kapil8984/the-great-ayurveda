using Ayurveda.Domain.Enums;

namespace Ayurveda.Domain.Entities;

public class Address
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public virtual ApplicationUser User { get; set; } = null!;

    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public AddressType AddressType { get; set; } = AddressType.Home;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WishlistItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public virtual ApplicationUser User { get; set; } = null!;

    public int ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
    public decimal DiscountAmount { get; set; }
    public decimal MinOrderAmount { get; set; } = 0.0m;
    public decimal MaxDiscountAmount { get; set; } = 0.0m;
    public DateTime? StartDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? ApplicableCategory { get; set; }
    public int UsageLimit { get; set; } = 1000;
    public int UsageCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Banner
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string Placement { get; set; } = "home_hero"; // home_hero, home_middle, shop, cart, doctors
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public virtual ApplicationUser User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public string Type { get; set; } = "Info"; // Info, Order, Appointment, Promo
    public string? TargetUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Payment
{
    public int Id { get; set; }
    public int? OrderId { get; set; }
    public int? AppointmentId { get; set; }
    public string Provider { get; set; } = "Razorpay";
    public string TransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Razorpay;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CallSession
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public virtual Appointment Appointment { get; set; } = null!;
    public string RoomName { get; set; } = string.Empty;
    public string Provider { get; set; } = "JitsiMeet";
    public string Status { get; set; } = "Scheduled"; // Scheduled, Active, Completed
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string? RecordingUrl { get; set; }
}

public class PasswordResetOtp
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string OtpHashed { get; set; } = string.Empty;
    public DateTime ExpiryUtc { get; set; }
    public int Attempts { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
