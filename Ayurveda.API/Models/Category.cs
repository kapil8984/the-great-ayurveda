namespace Ayurveda.API.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<Yoga> Yogas { get; set; } = new List<Yoga>();
    public virtual ICollection<DoctorDetail> DoctorSpecializations { get; set; } = new List<DoctorDetail>();
}
