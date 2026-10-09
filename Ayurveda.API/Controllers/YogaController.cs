using System.Security.Claims;
using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
public class YogaController : ControllerBase
{
    private readonly IYogaService _yogaService;
    private readonly IReviewService _reviewService;

    public YogaController(IYogaService yogaService, IReviewService reviewService)
    {
        _yogaService = yogaService;
        _reviewService = reviewService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return (claim != null && int.TryParse(claim.Value, out int id)) ? id : 0;
    }

    [HttpGet("yoga/getAll")]
    [HttpGet("api/yoga")]
    public async Task<IActionResult> GetAll()
    {
        var yogas = await _yogaService.GetAllYogasAsync();

        var legacyList = yogas.Select(y => new
        {
            _id = y.Id,
            id = y.Id,
            yogaName = y.YogaName,
            title = y.YogaName,
            Description = y.Description,
            instructions = y.Instructions,
            imageUrl = y.ImageUrl,
            videoUrl = y.VideoUrl,
            categoryId = y.CategoryId,
            category = new { name = y.CategoryName },
            benefits = y.Benefits,
            averageRating = y.AverageRating
        }).ToList();

        return Ok(new { Yogas = legacyList });
    }

    [HttpGet("api/yoga/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var yoga = await _yogaService.GetYogaByIdAsync(id);
        if (yoga == null) return NotFound(new { message = "Yoga not found" });
        return Ok(yoga);
    }

    [HttpPost("yoga/addYoga")]
    [HttpPost("api/yoga")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddYoga([FromBody] CreateYogaRequest request)
    {
        var yoga = await _yogaService.CreateYogaAsync(request);
        return Ok(new { message = "Yoga added", yoga, user = yoga });
    }

    [HttpPost("yoga/add-in-bulk-yoga")]
    [HttpPost("api/yoga/bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddInBulk([FromBody] List<CreateYogaRequest> requests)
    {
        foreach (var r in requests)
        {
            await _yogaService.CreateYogaAsync(r);
        }
        return Ok(new { message = "All Yoga saved..." });
    }

    public class UpdateYogaBody : UpdateYogaRequest
    {
        public int? Id { get; set; }
    }

    [HttpPut("yoga/updateYogaById/{id?}")]
    [HttpPut("yoga/updateYogaById")]
    [HttpPut("api/yoga/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateYoga(int? id, [FromBody] UpdateYogaBody request)
    {
        int targetId = id ?? request.Id ?? 0;
        if (targetId == 0) return BadRequest(new { error = "Yoga ID is required" });

        var updated = await _yogaService.UpdateYogaAsync(targetId, request);
        if (updated == null) return NotFound(new { error = "Yoga not found" });

        return Ok(new { message = "Yoga updated", yoga = updated });
    }

    [HttpDelete("yoga/removeYogaById/{id}")]
    [HttpDelete("api/yoga/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveYogaById(int id)
    {
        var success = await _yogaService.DeleteYogaAsync(id);
        if (!success) return NotFound(new { error = "Yoga not found" });
        return Ok(new { message = "Yoga removed" });
    }

    // Yoga Reviews
    [HttpPost("yoga/add-yoga-review")]
    [HttpPost("api/yoga/reviews")]
    [Authorize]
    public async Task<IActionResult> AddYogaReview([FromBody] AddYogaReviewRequest request)
    {
        // IDOR KILL: Ignore client UserId, derive strictly from authenticated principal
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { message = "Authentication required." });

        var success = await _reviewService.AddYogaReviewAsync(userId, request);
        return StatusCode(201, new { message = "Review added successfully." });
    }

    [HttpGet("yoga/get-review-for-yoga/{yogaId}")]
    [HttpGet("api/yoga/{yogaId}/reviews")]
    public async Task<IActionResult> GetReviewsForYoga(int yogaId)
    {
        var reviews = await _reviewService.GetYogaReviewsAsync(yogaId);

        var legacy = reviews.Select(r => new
        {
            _id = r.Id,
            rating = r.Rating,
            review = r.Review,
            user = new
            {
                _id = r.UserId,
                id = r.UserId,
                username = r.Username,
                email = r.UserEmail
            }
        }).ToList();

        return Ok(legacy);
    }

    [HttpGet("yoga/getYogaReview/{yogaId}/{userId}")]
    public async Task<IActionResult> GetYogaReviewByUser(int yogaId, int userId)
    {
        var review = await _reviewService.GetYogaReviewByUserAsync(yogaId, userId);
        if (review == null)
        {
            return NotFound(new { message = "No review found" });
        }

        return Ok(new
        {
            _id = review.Id,
            rating = review.Rating,
            review = review.Review,
            user = new
            {
                _id = review.UserId,
                username = review.Username,
                email = review.UserEmail
            }
        });
    }

    [HttpPut("yoga/updateYogaReview")]
    [Authorize]
    public async Task<IActionResult> UpdateYogaReview([FromBody] AddYogaReviewRequest request)
    {
        // IDOR KILL: Ignore client UserId, derive strictly from authenticated principal
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { message = "Authentication required." });

        var success = await _reviewService.UpdateYogaReviewAsync(userId, request);
        return Ok(new { message = "Review updated successfully." });
    }
}
