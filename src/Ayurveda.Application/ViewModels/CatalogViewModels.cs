namespace Ayurveda.Application.ViewModels;

public class ProductCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public int DiscountPercent { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int Stock { get; set; }
    public bool IsInStock => Stock > 0;
    public string? Badge { get; set; }
}

public class ProductDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? SubcategoryName { get; set; }
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public int DiscountPercent { get; set; }
    public int Stock { get; set; }
    public bool IsInStock => Stock > 0;
    public string ImageUrl { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public string? Sku { get; set; }
    public string? Tags { get; set; }
    public string? TraditionallyUsedFor { get; set; }
    public string? Ingredients { get; set; }
    public string? HowToUse { get; set; }
    public string? Precautions { get; set; }
    public bool IsPrescriptionRequired { get; set; }
    public List<string> AdditionalImages { get; set; } = new();
    public List<ProductReviewViewModel> Reviews { get; set; } = new();
    public List<ProductCardViewModel> RelatedProducts { get; set; } = new();
}

public class ProductReviewViewModel
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public int HelpfulVotes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CatalogListViewModel
{
    public List<ProductCardViewModel> Products { get; set; } = new();
    public List<CategoryFilterItem> Categories { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public int? SelectedCategoryId { get; set; }
    public string? SelectedSort { get; set; } = "popular";
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SearchQuery { get; set; }
    public string? Concern { get; set; }
}

public class CategoryFilterItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public bool IsSelected { get; set; }
}

public class HomeViewModel
{
    public string HeroVariant { get; set; } = "VaidyaHeritage";
    public List<ProductCardViewModel> FeaturedProducts { get; set; } = new();
    public List<ProductCardViewModel> BestSellers { get; set; } = new();
    public List<ProductCardViewModel> Deals { get; set; } = new();
    public List<CategoryCardViewModel> Categories { get; set; } = new();
    public List<DoctorCardViewModel> TopDoctors { get; set; } = new();
    public List<YogaCardViewModel> FeaturedYoga { get; set; } = new();
    public List<BannerItemViewModel> Banners { get; set; } = new();
}

public class CategoryCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int ProductCount { get; set; }
}

public class BannerItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
}
