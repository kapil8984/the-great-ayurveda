using System.Security.Claims;
using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return (claim != null && int.TryParse(claim.Value, out int id)) ? id : 0;
    }

    [HttpPost("cart/getCart")]
    [HttpPost("api/cart/get")]
    public async Task<IActionResult> FetchCartPost()
    {
        // IDOR KILL: Ignore request body UserId, derive strictly from JWT identity (Rule c)
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { error = "Authentication required." });

        var cart = await _cartService.GetUserCartAsync(userId);

        var legacyCartItems = cart.CartItems.Select(ci => new
        {
            ci.Id,
            productId = new
            {
                _id = ci.Product.Id,
                id = ci.Product.Id,
                title = ci.Product.Title,
                price = ci.Product.Price,
                description = ci.Product.Description,
                stock = ci.Product.Stock,
                brand = ci.Product.Brand,
                imageURL = ci.Product.ImageUrl,
                category = new { name = ci.Product.CategoryName }
            },
            quantity = ci.Quantity
        }).ToList();

        return Ok(new
        {
            cart = new
            {
                _id = cart.Id,
                userId = cart.UserId,
                cartItems = legacyCartItems,
                grandTotal = cart.GrandTotal
            }
        });
    }

    [HttpGet("api/cart")]
    public async Task<IActionResult> FetchCartGet()
    {
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { error = "Please login first" });

        var cart = await _cartService.GetUserCartAsync(userId);
        return Ok(cart);
    }

    [HttpPost("cart/add-to-cart")]
    [HttpPost("api/cart/items")]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
    {
        // IDOR KILL: Ignore request.UserId in body, derive strictly from JWT identity (Rule c)
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { error = "Authentication required." });

        var success = await _cartService.AddToCartAsync(userId, request.ProductId, request.Quantity);
        if (!success) return NotFound(new { error = "Product not found" });

        return Ok(new { message = "Item added to cart successfully" });
    }

    [HttpPut("cart/product-quantity")]
    [HttpPut("api/cart/quantity")]
    public async Task<IActionResult> UpdateQuantity([FromBody] UpdateCartQuantityRequest request)
    {
        // IDOR KILL: Ignore request.UserId in body, derive strictly from JWT identity (Rule c)
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { error = "Authentication required." });

        var success = await _cartService.UpdateQuantityAsync(userId, request.ProductId, request.Quantity);
        if (!success) return NotFound(new { error = "Item not found in cart" });

        return Ok(new { message = "Product quantity updated successfully" });
    }

    [HttpPost("cart/removeCart")]
    [HttpDelete("api/cart/items/{productId?}")]
    public async Task<IActionResult> RemoveCartItem([FromBody] RemoveCartItemRequest? request, int? productId)
    {
        int targetProductId = productId ?? request?.ProductId ?? 0;
        int userId = GetCurrentUserId();

        if (userId == 0 || targetProductId == 0)
        {
            return BadRequest(new { error = "Product ID is required." });
        }

        var success = await _cartService.RemoveCartItemAsync(userId, targetProductId);
        if (!success) return NotFound(new { error = "Item not found in cart" });

        return Ok(new { message = "Item removed from cart" });
    }
}
