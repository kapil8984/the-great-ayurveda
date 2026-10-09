namespace Ayurveda.API.Models;

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
