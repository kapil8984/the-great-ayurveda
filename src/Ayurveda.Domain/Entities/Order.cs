using Ayurveda.Domain.Enums;

namespace Ayurveda.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; } = 0.0m;
    public decimal DeliveryFee { get; set; } = 0.0m;
    public decimal TaxAmount { get; set; } = 0.0m;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Placed"; // Placed, Confirmed, Packed, Shipped, OutForDelivery, Delivered, Cancelled, ReturnRequested
    public string? ShippingAddress { get; set; }
    public string? PaymentMethod { get; set; } = "CashOnDelivery";
    public string PaymentStatus { get; set; } = "Pending";
    public string? TrackingNumber { get; set; }
    public string? CourierName { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string DeliveryType { get; set; } = "Standard"; // Standard, Express
    public string? CancellationReason { get; set; }
    public string? ReturnReason { get; set; }
    public string? ReturnStatus { get; set; } // None, Requested, Approved, Refunded
    public string? IdempotencyKey { get; set; }

    public virtual ApplicationUser? User { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.NotReviewed;

    public virtual Order Order { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
