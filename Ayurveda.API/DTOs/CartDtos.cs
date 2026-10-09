namespace Ayurveda.API.DTOs;

public record CartItemDto(
    int Id,
    int ProductId,
    ProductDto Product,
    int Quantity,
    decimal TotalPrice
);

public record CartDto(
    int Id,
    int UserId,
    List<CartItemDto> CartItems,
    decimal GrandTotal
);

public class AddToCartRequest
{
    public int? UserId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateCartQuantityRequest
{
    public int? UserId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; } // +1 or -1
}

public class RemoveCartItemRequest
{
    public int? UserId { get; set; }
    public int ProductId { get; set; }
}

public class FetchCartRequest
{
    public int? UserId { get; set; }
}
