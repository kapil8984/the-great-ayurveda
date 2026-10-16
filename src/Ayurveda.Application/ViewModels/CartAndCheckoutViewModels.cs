using Ayurveda.Domain.Enums;

namespace Ayurveda.Application.ViewModels;

public class CartViewModel
{
    public int CartId { get; set; }
    public List<CartItemViewModel> Items { get; set; } = new();
    public List<CartItemViewModel> SavedForLaterItems { get; set; } = new();
    
    public decimal Subtotal => Items.Sum(i => i.TotalPrice);
    public decimal MrpTotal => Items.Sum(i => i.Mrp * i.Quantity);
    public decimal ProductDiscountSavings => Math.Max(0, MrpTotal - Subtotal);
    public decimal CouponDiscount { get; set; }
    public decimal TotalDiscount => ProductDiscountSavings + CouponDiscount;
    
    public decimal FreeShippingThreshold { get; set; } = 499.00m;
    public decimal DeliveryFee => Subtotal >= FreeShippingThreshold || Subtotal == 0 ? 0.00m : 40.00m;
    public decimal TaxAmount => Math.Round(Math.Max(0, Subtotal - CouponDiscount) * 0.05m, 2); // 5% GST on ayurvedic medicines
    public decimal GrandTotal => Math.Max(0, Subtotal - CouponDiscount + DeliveryFee + TaxAmount);
    public decimal AmountNeededForFreeShipping => Math.Max(0, FreeShippingThreshold - Subtotal);
    
    public string? AppliedCouponCode { get; set; }
    public string? CouponError { get; set; }
    public string? CouponSuccess { get; set; }
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
    public bool IsInStock => Stock >= Quantity;
    public decimal TotalPrice => UnitPrice * Quantity;
}

public class CheckoutViewModel
{
    public List<AddressViewModel> SavedAddresses { get; set; } = new();
    public int? SelectedAddressId { get; set; }
    public AddressViewModel NewAddress { get; set; } = new();
    public PaymentMethod SelectedPaymentMethod { get; set; } = PaymentMethod.Razorpay;
    public string DeliveryType { get; set; } = "Standard"; // "Standard" or "Express"
    public decimal ExpressDeliveryFee { get; set; } = 99.00m;
    public CartViewModel Cart { get; set; } = new();
    public string? OrderNotes { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayOrderId { get; set; }
    public bool SimulateFailure { get; set; } = false;
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
    public DateTime? DeliveryDate { get; set; }
    public string DeliveryType { get; set; } = "Standard";
    
    public bool CanCancel => Status == "Placed" || Status == "Confirmed";
    public bool CanReturn => Status == "Delivered" && (ReturnStatus == null || ReturnStatus == "None");
    public string? CancellationReason { get; set; }
    public string? ReturnReason { get; set; }
    public string? ReturnStatus { get; set; }
    
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
