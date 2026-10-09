namespace Ayurveda.API.DTOs;

public record ProductDto(
    int Id,
    string Title,
    string Description,
    decimal Price,
    int Stock,
    string Brand,
    string ImageUrl,
    int CategoryId,
    string? CategoryName,
    double AverageRating,
    int ReviewCount,
    DateTime CreatedAt
);

public class CreateProductRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Brand { get; set; } = string.Empty;
    public int Category { get; set; }
    public IFormFile? Image { get; set; }
    public string? ImageUrl { get; set; }
}

public class UpdateProductRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public int? Stock { get; set; }
    public string? Brand { get; set; }
    public int? Category { get; set; }
    public IFormFile? Image { get; set; }
}

public class ProductQueryParameters
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public double? MinRating { get; set; }
    public string? SortBy { get; set; } // "price_asc", "price_desc", "rating", "newest"
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);
