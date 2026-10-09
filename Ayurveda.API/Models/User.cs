namespace Ayurveda.API.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string? ProfileImage { get; set; }
    public string Role { get; set; } = "User";
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Cart? Cart { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
    public virtual ICollection<YogaReview> YogaReviews { get; set; } = new List<YogaReview>();
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
