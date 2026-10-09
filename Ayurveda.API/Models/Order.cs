namespace Ayurveda.API.Models;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Placed"; // Placed, Confirmed, Shipped, Delivered, Cancelled
    public string? ShippingAddress { get; set; }
    public string? PaymentMethod { get; set; } = "CashOnDelivery";

    public virtual User User { get; set; } = null!;
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
