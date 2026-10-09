namespace Ayurveda.API.Models;

public class YogaReview
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int YogaId { get; set; }
    public int Rating { get; set; } // 1 - 5
    public string ReviewText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual Yoga Yoga { get; set; } = null!;
}
