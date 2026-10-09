namespace Ayurveda.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public int DiscountPercent { get; set; }
    public int Stock { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public double AverageRating { get; set; } = 0.0;
    public string? Tags { get; set; }
    public string? TraditionallyUsedFor { get; set; }
    public string? Ingredients { get; set; }
    public string? HowToUse { get; set; }
    public string? Precautions { get; set; }
    public bool IsPrescriptionRequired { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Category Category { get; set; } = null!;
    public virtual Subcategory? Subcategory { get; set; }
    public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public virtual ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? SizeOrPack { get; set; }
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}
