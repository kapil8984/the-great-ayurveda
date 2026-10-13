using Microsoft.AspNetCore.Identity;

namespace Ayurveda.Domain.Entities;

public class ApplicationUser : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public string? ProfileImageUrl { get; set; }

    // Navigations
    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual Cart? Cart { get; set; }
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
    public virtual ICollection<YogaReview> YogaReviews { get; set; } = new List<YogaReview>();
    public virtual ICollection<WishlistItem> Wishlist { get; set; } = new List<WishlistItem>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual Doctor? DoctorProfile { get; set; }
}
