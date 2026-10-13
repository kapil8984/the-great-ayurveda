namespace Ayurveda.Domain.Enums;

public enum UserRole
{
    Customer,
    Doctor,
    SuperAdmin,
    CatalogManager,
    SupportAgent,
    FinanceManager
}

public enum DoctorStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum OrderStatus
{
    Placed = 0,
    Packed = 1,
    Shipped = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Cancelled = 5,
    ReturnRequested = 6,
    Returned = 7
}

public enum ReviewStatus
{
    NotReviewed = 0,
    Reviewed = 1
}

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    Razorpay = 1
}

public enum AppointmentStatus
{
    Scheduled = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3,
    Declined = 4
}

public enum ConsultationType
{
    Audio = 0,
    Video = 1,
    Chat = 2
}

public enum DiscountType
{
    Percentage = 0,
    Flat = 1
}

public enum AddressType
{
    Home = 0,
    Work = 1,
    Other = 2
}
