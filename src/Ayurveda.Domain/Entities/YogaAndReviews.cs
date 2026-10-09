namespace Ayurveda.Domain.Entities;

public class Yoga
{
    public int Id { get; set; }
    public string YogaName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string BenefitsJson { get; set; } = "[]";
    public double AverageRating { get; set; } = 0.0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Category Category { get; set; } = null!;
    public virtual ICollection<YogaReview> Reviews { get; set; } = new List<YogaReview>();
}

public class YogaReview
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int YogaId { get; set; }
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Yoga Yoga { get; set; } = null!;
}

public class ProductReview
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public bool IsVerifiedPurchase { get; set; } = false;
    public int HelpfulVotes { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
