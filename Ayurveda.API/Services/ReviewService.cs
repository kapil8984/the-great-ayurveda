using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface IReviewService
{
    Task<bool> AddProductReviewAsync(int userId, AddProductReviewRequest request);
    Task<List<ProductReviewDto>> GetProductReviewsAsync(int productId);
    Task<bool> UpdateProductReviewAsync(int userId, AddProductReviewRequest request);
    Task<bool> DeleteProductReviewAsync(int reviewId, int currentUserId = 0, bool isAdmin = false);

    Task<bool> AddYogaReviewAsync(int userId, AddYogaReviewRequest request);
    Task<List<YogaReviewDto>> GetYogaReviewsAsync(int yogaId);
    Task<YogaReviewDto?> GetYogaReviewByUserAsync(int yogaId, int userId);
    Task<bool> UpdateYogaReviewAsync(int userId, AddYogaReviewRequest request);
}

public class ReviewService : IReviewService
{
    private readonly AyurvedaDbContext _context;
    private readonly IProductService _productService;
    private readonly IYogaService _yogaService;

    public ReviewService(AyurvedaDbContext context, IProductService productService, IYogaService yogaService)
    {
        _context = context;
        _productService = productService;
        _yogaService = yogaService;
    }

    public async Task<bool> AddProductReviewAsync(int userId, AddProductReviewRequest request)
    {
        if (await _context.ProductReviews.AnyAsync(r => r.UserId == userId && r.ProductId == request.ProductId))
        {
            return await UpdateProductReviewAsync(userId, request);
        }

        var review = new ProductReview
        {
            UserId = userId,
            ProductId = request.ProductId,
            Rating = Math.Clamp(request.Rating, 1, 5),
            ReviewText = request.Review.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductReviews.Add(review);

        // Mark review flag on relevant order item if user ordered it
        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.ProductId == request.ProductId && oi.Order.UserId == userId)
            .ToListAsync();

        foreach (var oi in orderItems)
        {
            oi.ReviewStatus = 1;
        }

        await _context.SaveChangesAsync();
        await _productService.RecalculateRatingAsync(request.ProductId);
        return true;
    }

    public async Task<List<ProductReviewDto>> GetProductReviewsAsync(int productId)
    {
        return await _context.ProductReviews
            .Include(pr => pr.User)
            .Where(pr => pr.ProductId == productId)
            .OrderByDescending(pr => pr.CreatedAt)
            .Select(pr => new ProductReviewDto(
                pr.Id,
                pr.UserId,
                pr.User.Username,
                pr.User.Email,
                pr.ProductId,
                pr.Rating,
                pr.ReviewText,
                pr.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<bool> UpdateProductReviewAsync(int userId, AddProductReviewRequest request)
    {
        var review = await _context.ProductReviews
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == request.ProductId);

        if (review == null)
        {
            return await AddProductReviewAsync(userId, request);
        }

        review.Rating = Math.Clamp(request.Rating, 1, 5);
        review.ReviewText = request.Review.Trim();

        await _context.SaveChangesAsync();
        await _productService.RecalculateRatingAsync(request.ProductId);
        return true;
    }

    public async Task<bool> DeleteProductReviewAsync(int reviewId, int currentUserId = 0, bool isAdmin = false)
    {
        var review = await _context.ProductReviews.FindAsync(reviewId);
        if (review == null) return false;

        if (!isAdmin && currentUserId > 0 && review.UserId != currentUserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this review.");
        }

        var productId = review.ProductId;
        _context.ProductReviews.Remove(review);
        await _context.SaveChangesAsync();
        await _productService.RecalculateRatingAsync(productId);
        return true;
    }

    public async Task<bool> AddYogaReviewAsync(int userId, AddYogaReviewRequest request)
    {
        if (await _context.YogaReviews.AnyAsync(r => r.UserId == userId && r.YogaId == request.YogaId))
        {
            return await UpdateYogaReviewAsync(userId, request);
        }

        var review = new YogaReview
        {
            UserId = userId,
            YogaId = request.YogaId,
            Rating = Math.Clamp(request.Rating, 1, 5),
            ReviewText = request.Review.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.YogaReviews.Add(review);
        await _context.SaveChangesAsync();
        await _yogaService.RecalculateRatingAsync(request.YogaId);
        return true;
    }

    public async Task<List<YogaReviewDto>> GetYogaReviewsAsync(int yogaId)
    {
        return await _context.YogaReviews
            .Include(yr => yr.User)
            .Where(yr => yr.YogaId == yogaId)
            .OrderByDescending(yr => yr.CreatedAt)
            .Select(yr => new YogaReviewDto(
                yr.Id,
                yr.UserId,
                yr.User.Username,
                yr.User.Email,
                yr.YogaId,
                yr.Rating,
                yr.ReviewText,
                yr.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<YogaReviewDto?> GetYogaReviewByUserAsync(int yogaId, int userId)
    {
        var review = await _context.YogaReviews
            .Include(yr => yr.User)
            .FirstOrDefaultAsync(yr => yr.YogaId == yogaId && yr.UserId == userId);

        return review == null ? null : new YogaReviewDto(
            review.Id,
            review.UserId,
            review.User.Username,
            review.User.Email,
            review.YogaId,
            review.Rating,
            review.ReviewText,
            review.CreatedAt
        );
    }

    public async Task<bool> UpdateYogaReviewAsync(int userId, AddYogaReviewRequest request)
    {
        var review = await _context.YogaReviews
            .FirstOrDefaultAsync(yr => yr.YogaId == request.YogaId && yr.UserId == userId);

        if (review == null)
        {
            return await AddYogaReviewAsync(userId, request);
        }

        review.Rating = Math.Clamp(request.Rating, 1, 5);
        review.ReviewText = request.Review.Trim();

        await _context.SaveChangesAsync();
        await _yogaService.RecalculateRatingAsync(request.YogaId);
        return true;
    }
}
