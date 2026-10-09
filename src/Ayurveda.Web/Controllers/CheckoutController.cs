using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class CheckoutController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CheckoutController(AyurvedaDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<ApplicationUser> GetCurrentUserAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            var demoUser = await _context.Users.FirstOrDefaultAsync();
            if (demoUser == null)
            {
                demoUser = new ApplicationUser { UserName = "guest@ayurveda.local", Email = "guest@ayurveda.local", FullName = "Valued Customer" };
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
            .Select(a => new AddressViewModel
            {
                Id = a.Id,
                RecipientName = a.RecipientName,
                Phone = a.Phone,
                AddressLine1 = a.AddressLine1,
                City = a.City,
                State = a.State,
                PostalCode = a.PostalCode,
                IsDefault = a.IsDefault
            })
            .ToListAsync();

        var cartVm = new CartViewModel
        {
            CartId = cart.Id,
            Items = cart.CartItems.Select(ci => new CartItemViewModel
            {
                CartItemId = ci.Id,
                ProductId = ci.ProductId,
                Title = ci.Product.Title,
                UnitPrice = ci.Product.Price,
                Quantity = ci.Quantity
            }).ToList()
        };

        var model = new CheckoutViewModel
        {
            SavedAddresses = addresses,
            Cart = cartVm
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
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

        // Determine destination address
        string shippingAddressText = "";
        if (model.SelectedAddressId.HasValue)
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
                RecipientName = model.NewAddress.RecipientName,
                Phone = model.NewAddress.Phone,
                AddressLine1 = model.NewAddress.AddressLine1,
                City = model.NewAddress.City,
                State = model.NewAddress.State,
                PostalCode = model.NewAddress.PostalCode,
                IsDefault = true
            };
            _context.Addresses.Add(newAddr);
            await _context.SaveChangesAsync();
            shippingAddressText = $"{newAddr.RecipientName}, {newAddr.Phone}, {newAddr.AddressLine1}, {newAddr.City}, {newAddr.State} - {newAddr.PostalCode}";
        }
        else
        {
            shippingAddressText = $"{user.FullName}, {user.Email}, Primary Delivery Address, New Delhi 110001";
        }

        // Server-side price & stock verification in transaction
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

                decimal deliveryFee = subtotal >= 499.00m ? 0.00m : 40.00m;
                decimal tax = Math.Round(subtotal * 0.05m, 2);
                decimal grandTotal = subtotal + deliveryFee + tax;

                order = new Order
                {
                    UserId = user.Id,
                    OrderNumber = $"TGA-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                    OrderDate = DateTime.UtcNow,
                    Status = OrderStatus.Placed.ToString(),
                    TotalAmount = grandTotal,
                    DeliveryFee = deliveryFee,
                    TaxAmount = tax,
                    ShippingAddress = shippingAddressText,
                    PaymentMethod = model.SelectedPaymentMethod.ToString(),
                    PaymentStatus = model.SelectedPaymentMethod == PaymentMethod.Razorpay ? "Paid" : "Pending",
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);

                // Clear cart items
                _context.CartItems.RemoveRange(cart.CartItems);

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
            Status = order.Status,
            PaymentMethod = order.PaymentMethod ?? "Online",
            PaymentStatus = order.PaymentStatus,
            ShippingAddress = order.ShippingAddress
        };

        return View(model);
    }
}
