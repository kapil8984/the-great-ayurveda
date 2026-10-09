namespace Ayurveda.API.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; } // Historical price at time of order
    public int ReviewStatus { get; set; } = 0; // 0: not reviewed, 1: reviewed

    public virtual Order Order { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
