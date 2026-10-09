namespace Ayurveda.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<Yoga> Yogas { get; set; } = new List<Yoga>();
    public virtual ICollection<DoctorDetail> DoctorSpecializations { get; set; } = new List<DoctorDetail>();
}

public class Subcategory
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public virtual Category Category { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
