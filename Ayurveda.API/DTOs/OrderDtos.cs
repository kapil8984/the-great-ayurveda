namespace Ayurveda.API.DTOs;

public record OrderItemDto(
    int Id,
    int ProductId,
    ProductDto? Product,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    int Review
);

public record OrderDto(
    int Id,
    int UserId,
    decimal TotalAmount,
    DateTime OrderDate,
    string Status,
    string? ShippingAddress,
    string? PaymentMethod,
    List<OrderItemDto> Products
);

public class OrderProductInput
{
    public int? ProductId { get; set; }
    public object? Product { get; set; } // Handles both { productId: 1 } and { product: 1 } from legacy frontend
    public int Quantity { get; set; } = 1;
}

public class CreateOrderRequest
{
    public int? UserId { get; set; }
    public List<OrderProductInput>? Products { get; set; }
    public string? ShippingAddress { get; set; }
    public string? PaymentMethod { get; set; }
}

public class GetOrdersRequest
{
    public int? UserId { get; set; }
}

public record UpdateOrderStatusRequest(string Status);
