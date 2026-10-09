using Ayurveda.Domain.Enums;

namespace Ayurveda.Application.ViewModels;

public class CartViewModel
{
    public int CartId { get; set; }
    public List<CartItemViewModel> Items { get; set; } = new();
    public decimal Subtotal => Items.Sum(i => i.TotalPrice);
    public decimal DiscountAmount { get; set; }
    public decimal FreeShippingThreshold { get; set; } = 499.00m;
    public decimal DeliveryFee => Subtotal >= FreeShippingThreshold || Subtotal == 0 ? 0.00m : 40.00m;
    public decimal TaxAmount => Math.Round(Subtotal * 0.05m, 2); // 5% GST on ayurvedic medicines
    public decimal GrandTotal => Math.Max(0, Subtotal - DiscountAmount + DeliveryFee + TaxAmount);
    public decimal AmountNeededForFreeShipping => Math.Max(0, FreeShippingThreshold - Subtotal);
    public string? AppliedCouponCode { get; set; }
}

public class CartItemViewModel
{
    public int CartItemId { get; set; }
    public int ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Mrp { get; set; }
    public int Quantity { get; set; }
    public int Stock { get; set; }
    public decimal TotalPrice => UnitPrice * Quantity;
}

public class CheckoutViewModel
{
    public List<AddressViewModel> SavedAddresses { get; set; } = new();
    public int? SelectedAddressId { get; set; }
    public AddressViewModel NewAddress { get; set; } = new();
    public PaymentMethod SelectedPaymentMethod { get; set; } = PaymentMethod.Razorpay;
    public CartViewModel Cart { get; set; } = new();
    public string? OrderNotes { get; set; }
}

public class AddressViewModel
{
    public int Id { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public AddressType AddressType { get; set; } = AddressType.Home;
}

public class OrderSummaryViewModel
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public string? FirstItemTitle { get; set; }
    public string? FirstItemImage { get; set; }
}

public class OrderDetailViewModel
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TaxAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? ShippingAddress { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CourierName { get; set; }
    public List<OrderItemViewModel> Items { get; set; } = new();
    public List<OrderTimelineStep> Timeline { get; set; } = new();
}

public class OrderItemViewModel
{
    public int ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice => UnitPrice * Quantity;
}

public class OrderTimelineStep
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime? Timestamp { get; set; }
}
