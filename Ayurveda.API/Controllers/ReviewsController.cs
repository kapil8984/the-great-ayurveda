using System.Security.Claims;
using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return (claim != null && int.TryParse(claim.Value, out int id)) ? id : 0;
    }

    [HttpPost("rate-review/add-user-review-and-rating")]
    [HttpPost("api/products/reviews")]
    [Authorize]
    public async Task<IActionResult> AddProductReview([FromBody] AddProductReviewRequest request)
    {
        // IDOR KILL: Ignore request.UserId in body, derive strictly from authenticated principal
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { message = "Authentication required to review products." });

        var success = await _reviewService.AddProductReviewAsync(userId, request);
        return StatusCode(201, new { message = "Rating and review added successfully" });
    }

    [HttpPost("rate-review/get-product-review")]
    public async Task<IActionResult> GetProductReviewsPost([FromBody] GetProductReviewRequest request)
    {
        var reviews = await _reviewService.GetProductReviewsAsync(request.ProductId);

        var legacy = reviews.Select(r => new
        {
            _id = r.Id,
            user = new
            {
                _id = r.UserId,
                username = r.Username,
                email = r.UserEmail
            },
            review = r.Review,
            rating = r.Rating
        }).ToList();

        return Ok(legacy);
    }

    [HttpGet("api/products/{productId}/reviews")]
    public async Task<IActionResult> GetProductReviewsGet(int productId)
    {
        var reviews = await _reviewService.GetProductReviewsAsync(productId);
        return Ok(reviews);
    }

    [HttpPut("rate-review/change-review")]
    [HttpPut("api/products/reviews")]
    [Authorize]
    public async Task<IActionResult> UpdateProductReview([FromBody] AddProductReviewRequest request)
    {
        // IDOR KILL: Ignore request.UserId in body, derive strictly from authenticated principal
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { message = "Authentication required." });

        var success = await _reviewService.UpdateProductReviewAsync(userId, request);
        return Ok(new { message = "Rating and review updated successfully" });
    }

    [HttpDelete("rate-review/remove-rating/{ratingId}")]
    [HttpDelete("api/products/reviews/{ratingId}")]
    [Authorize]
    public async Task<IActionResult> RemoveProductReview(int ratingId)
    {
        int userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        try
        {
            var success = await _reviewService.DeleteProductReviewAsync(ratingId, userId, isAdmin);
            if (!success) return NotFound(new { message = "Rating not found" });
            return Ok(new { message = "Rating removed successfully" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
