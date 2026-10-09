namespace Ayurveda.API.Models;

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;

    public virtual Cart Cart { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
