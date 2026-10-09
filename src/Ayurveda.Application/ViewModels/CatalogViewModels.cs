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
    public string? Sku { get; set; }
    public string? TraditionallyUsedFor { get; set; }
    public bool IsInWishlist { get; set; }
}

public class ProductVariantViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? SizeOrPack { get; set; }
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProductFaqViewModel
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
    public string? AskedBy { get; set; }
    public string? AnsweredBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? SubcategoryName { get; set; }
    public int? SubcategoryId { get; set; }
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
    public bool IsInWishlist { get; set; }
    public List<string> AdditionalImages { get; set; } = new();
    public List<ProductVariantViewModel> Variants { get; set; } = new();
    public List<ProductReviewViewModel> Reviews { get; set; } = new();
    public List<ProductFaqViewModel> Questions { get; set; } = new();
    public List<ProductCardViewModel> RelatedProducts { get; set; } = new();
    public ProductCardViewModel? FrequentlyBoughtTogether { get; set; }
    public List<ProductCardViewModel> RecentlyViewed { get; set; } = new();
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

public class ActiveFilterChip
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string RemoveUrl { get; set; } = string.Empty;
}

public class CategoryFilterItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public bool IsSelected { get; set; }
}

public class SubcategoryFilterItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public bool IsSelected { get; set; }
}

public class BrandFilterItem
{
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public bool IsSelected { get; set; }
}

public class CatalogListViewModel
{
    public List<ProductCardViewModel> Products { get; set; } = new();
    public List<CategoryFilterItem> Categories { get; set; } = new();
    public List<SubcategoryFilterItem> Subcategories { get; set; } = new();
    public List<BrandFilterItem> Brands { get; set; } = new();
    public List<ActiveFilterChip> ActiveChips { get; set; } = new();
    public List<string> AvailableConcerns { get; set; } = new();

    public int TotalCount { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Filter properties
    public int? SelectedCategoryId { get; set; }
    public int? SelectedSubcategoryId { get; set; }
    public string? SelectedBrand { get; set; }
    public string? SelectedSort { get; set; } = "popular";
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public double? MinRating { get; set; }
    public int? MinDiscount { get; set; }
    public bool InStockOnly { get; set; }
    public string? SearchQuery { get; set; }
    public string? Concern { get; set; }
    public string ViewMode { get; set; } = "grid"; // "grid" or "list"
}

public class WishlistViewModel
{
    public List<WishlistItemViewModel> Items { get; set; } = new();
    public int TotalCount => Items.Count;
}

public class WishlistItemViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public int DiscountPercent { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsInStock { get; set; }
    public double AverageRating { get; set; }
    public DateTime AddedAt { get; set; }
}

public class CompareViewModel
{
    public List<CompareProductViewModel> Products { get; set; } = new();
}

public class CompareProductViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int Stock { get; set; }
    public string? TraditionallyUsedFor { get; set; }
    public string? Ingredients { get; set; }
    public string? HowToUse { get; set; }
    public string? Precautions { get; set; }
}

public class PincodeCheckResultViewModel
{
    public string Pincode { get; set; } = string.Empty;
    public bool IsServiceable { get; set; }
    public string? EstimatedDeliveryDate { get; set; }
    public string? CityState { get; set; }
    public bool IsCodAvailable { get; set; }
    public string? Message { get; set; }
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
