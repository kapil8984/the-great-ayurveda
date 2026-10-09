namespace Ayurveda.API.DTOs;

public record ProductReviewDto(
    int Id,
    int UserId,
    string Username,
    string? UserEmail,
    int ProductId,
    int Rating,
    string Review,
    DateTime CreatedAt
);

public class AddProductReviewRequest
{
    public int? UserId { get; set; }
    public int ProductId { get; set; }
    public int Rating { get; set; }
    public string Review { get; set; } = string.Empty;
}

public class GetProductReviewRequest
{
    public int ProductId { get; set; }
}

public record YogaReviewDto(
    int Id,
    int UserId,
    string Username,
    string? UserEmail,
    int YogaId,
    int Rating,
    string Review,
    DateTime CreatedAt
);

public class AddYogaReviewRequest
{
    public int? UserId { get; set; }
    public int YogaId { get; set; }
    public int Rating { get; set; }
    public string Review { get; set; } = string.Empty;
}
