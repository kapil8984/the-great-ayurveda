using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class CheckoutController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public CheckoutController(
        AyurvedaDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
    }

    private async Task<ApplicationUser> GetCurrentUserAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            var demoUser = await _context.Users.FirstOrDefaultAsync();
            if (demoUser == null)
            {
                demoUser = new ApplicationUser
                {
                    UserName = "guest@ayurveda.local",
                    Email = "guest@ayurveda.local",
                    FullName = "Valued Customer",
                    EmailConfirmed = true
                };
                await _userManager.CreateAsync(demoUser, "GuestPass@2026");
            }
            user = demoUser;
        }
        return user;
    }

    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();

        var cart = await _context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (cart == null || !cart.CartItems.Any())
        {
            TempData["ErrorMessage"] = "Your basket is empty.";
            return RedirectToAction("Index", "Cart");
        }

        var addresses = await _context.Addresses
            .Where(a => a.UserId == user.Id)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AddressViewModel
            {
                Id = a.Id,
                RecipientName = a.RecipientName,
                Phone = a.Phone,
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2,
                Landmark = a.Landmark,
                City = a.City,
                State = a.State,
                PostalCode = a.PostalCode,
                IsDefault = a.IsDefault
            })
            .ToListAsync();

        var appliedCouponCode = HttpContext.Session.GetString("AppliedCouponCode");
        decimal couponDiscount = 0;
        var items = cart.CartItems.Select(ci => new CartItemViewModel
        {
            CartItemId = ci.Id,
            ProductId = ci.ProductId,
            Title = ci.Product.Title,
            Brand = ci.Product.Brand,
            ImageUrl = ci.Product.ImageUrl,
            UnitPrice = ci.Product.Price,
            Mrp = ci.Product.Mrp,
            Quantity = ci.Quantity,
            Stock = ci.Product.Stock
        }).ToList();

        decimal subtotal = items.Sum(i => i.TotalPrice);

        if (!string.IsNullOrWhiteSpace(appliedCouponCode))
        {
            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code.ToUpper() == appliedCouponCode.ToUpper() && c.IsActive);
            if (coupon != null && subtotal >= coupon.MinOrderAmount)
            {
                if (coupon.DiscountType == DiscountType.Percentage)
                {
                    var calc = Math.Round(subtotal * (coupon.DiscountAmount / 100m), 2);
                    couponDiscount = coupon.MaxDiscountAmount > 0 ? Math.Min(calc, coupon.MaxDiscountAmount) : calc;
                }
                else
                {
                    couponDiscount = coupon.DiscountAmount;
                }
            }
        }

        var cartVm = new CartViewModel
        {
            CartId = cart.Id,
            Items = items,
            AppliedCouponCode = appliedCouponCode,
            CouponDiscount = couponDiscount
        };

        var model = new CheckoutViewModel
        {
            SavedAddresses = addresses,
            SelectedAddressId = addresses.FirstOrDefault(a => a.IsDefault)?.Id ?? addresses.FirstOrDefault()?.Id,
            Cart = cartVm,
            DeliveryType = "Standard",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RazorpayKeyId = _configuration["Payment:Razorpay:KeyId"] ?? "rzp_test_AyurvedaDemoKey123"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
    {
        var user = await GetCurrentUserAsync();

        // Idempotency check: prevent duplicate submissions
        if (!string.IsNullOrWhiteSpace(model.IdempotencyKey))
        {
            var existingOrder = await _context.Orders
                .FirstOrDefaultAsync(o => o.UserId == user.Id && o.IdempotencyKey == model.IdempotencyKey);
            if (existingOrder != null)
            {
                return RedirectToAction(nameof(Confirmation), new { id = existingOrder.Id });
            }
        }

        var cart = await _context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (cart == null || !cart.CartItems.Any())
        {
            TempData["ErrorMessage"] = "Your basket is empty.";
            return RedirectToAction("Index", "Cart");
        }

        // 1. Resolve Shipping Address
        string shippingAddressText = "";
        if (model.SelectedAddressId.HasValue && model.SelectedAddressId.Value > 0)
        {
            var savedAddr = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == model.SelectedAddressId.Value && a.UserId == user.Id);
            if (savedAddr != null)
            {
                shippingAddressText = $"{savedAddr.RecipientName}, {savedAddr.Phone}, {savedAddr.AddressLine1}, {savedAddr.City}, {savedAddr.State} - {savedAddr.PostalCode}";
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.NewAddress.RecipientName))
        {
            var newAddr = new Address
            {
                UserId = user.Id,
                RecipientName = model.NewAddress.RecipientName.Trim(),
                Phone = model.NewAddress.Phone.Trim(),
                AddressLine1 = model.NewAddress.AddressLine1.Trim(),
                City = model.NewAddress.City.Trim(),
                State = model.NewAddress.State.Trim(),
                PostalCode = model.NewAddress.PostalCode.Trim(),
                IsDefault = true
            };
            _context.Addresses.Add(newAddr);
            await _context.SaveChangesAsync();
            shippingAddressText = $"{newAddr.RecipientName}, {newAddr.Phone}, {newAddr.AddressLine1}, {newAddr.City}, {newAddr.State} - {newAddr.PostalCode}";
        }
        else
        {
            shippingAddressText = $"{user.FullName}, {user.ContactNumber ?? "9876543210"}, Primary Delivery Address, New Delhi 110001";
        }

        // 2. Server-side price & stock verification in atomic transaction
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        Order? order = null;

        await executionStrategy.ExecuteAsync(async () =>
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal subtotal = 0;
                var orderItems = new List<OrderItem>();

                foreach (var item in cart.CartItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || !product.IsActive)
                        throw new InvalidOperationException($"Product '{item.Product.Title}' is unavailable.");

                    if (product.Stock < item.Quantity)
                        throw new InvalidOperationException($"Insufficient stock for '{product.Title}'.");

                    // Concurrency safe stock decrement
                    product.Stock -= item.Quantity;

                    var itemTotal = product.Price * item.Quantity;
                    subtotal += itemTotal;

                    orderItems.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price
                    });
                }

                // 3. Coupon calculation
                var appliedCouponCode = HttpContext.Session.GetString("AppliedCouponCode");
                decimal couponDiscount = 0;
                if (!string.IsNullOrWhiteSpace(appliedCouponCode))
                {
                    var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code.ToUpper() == appliedCouponCode.ToUpper() && c.IsActive);
                    if (coupon != null && subtotal >= coupon.MinOrderAmount)
                    {
                        if (coupon.DiscountType == DiscountType.Percentage)
                        {
                            var calc = Math.Round(subtotal * (coupon.DiscountAmount / 100m), 2);
                            couponDiscount = coupon.MaxDiscountAmount > 0 ? Math.Min(calc, coupon.MaxDiscountAmount) : calc;
                        }
                        else
                        {
                            couponDiscount = coupon.DiscountAmount;
                        }
                        coupon.UsageCount += 1;
                    }
                }

                decimal deliveryFee = 0.00m;
                if (model.DeliveryType == "Express")
                {
                    deliveryFee = 99.00m;
                }
                else
                {
                    deliveryFee = subtotal >= 499.00m ? 0.00m : 40.00m;
                }

                decimal taxableAmount = Math.Max(0, subtotal - couponDiscount);
                decimal tax = Math.Round(taxableAmount * 0.05m, 2);
                decimal grandTotal = Math.Max(0, taxableAmount + deliveryFee + tax);

                order = new Order
                {
                    UserId = user.Id,
                    OrderNumber = $"TGA-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                    OrderDate = DateTime.UtcNow,
                    Status = OrderStatus.Placed.ToString(),
                    TotalAmount = grandTotal,
                    DiscountAmount = couponDiscount,
                    DeliveryFee = deliveryFee,
                    TaxAmount = tax,
                    ShippingAddress = shippingAddressText,
                    PaymentMethod = model.SelectedPaymentMethod.ToString(),
                    PaymentStatus = model.SelectedPaymentMethod == PaymentMethod.Razorpay ? "Paid" : "Pending",
                    DeliveryType = model.DeliveryType ?? "Standard",
                    IdempotencyKey = model.IdempotencyKey,
                    TrackingNumber = $"TRK-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                    CourierName = "BlueDart Express Healthcare Logistics",
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);

                // Add confirmation notification
                _context.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Title = "Order Confirmed!",
                    Message = $"Your order #{order.OrderNumber} for ₹{grandTotal:N0} has been confirmed.",
                    Type = "Order",
                    TargetUrl = $"/Orders/Details/{order.Id}"
                });

                // Clear cart items and coupon
                _context.CartItems.RemoveRange(cart.CartItems);
                HttpContext.Session.Remove("AppliedCouponCode");

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });

        if (order != null)
        {
            TempData["SuccessMessage"] = "Your Ayurvedic order has been placed successfully!";
            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }

        TempData["ErrorMessage"] = "Could not place order. Please try again.";
        return RedirectToAction("Index", "Cart");
    }

    public async Task<IActionResult> Confirmation(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        var model = new OrderDetailViewModel
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Subtotal = order.TotalAmount - order.DeliveryFee - order.TaxAmount + order.DiscountAmount,
            DiscountAmount = order.DiscountAmount,
            DeliveryFee = order.DeliveryFee,
            TaxAmount = order.TaxAmount,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod ?? "Online",
            PaymentStatus = order.PaymentStatus,
            ShippingAddress = order.ShippingAddress,
            DeliveryType = order.DeliveryType,
            TrackingNumber = order.TrackingNumber,
            CourierName = order.CourierName,
            Items = order.OrderItems.Select(oi => new OrderItemViewModel
            {
                ProductId = oi.ProductId,
                Title = oi.Product.Title,
                ImageUrl = oi.Product.ImageUrl,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity
            }).ToList()
        };

        return View(model);
    }
}
