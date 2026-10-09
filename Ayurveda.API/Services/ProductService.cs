using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductQueryParameters query);
    Task<List<ProductDto>> GetAllProductsAsync();
    Task<ProductDto?> GetProductByIdAsync(int id);
    Task<List<ProductDto>> GetProductsByCategoryAsync(int categoryId);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request);
    Task<ProductDto?> UpdateProductAsync(int id, UpdateProductRequest request);
    Task<bool> DeleteProductAsync(int id);
    Task<int> SaveInBulkAsync(List<CreateProductRequest> requests);
    Task RecalculateRatingAsync(int productId);
}

public class ProductService : IProductService
{
    private readonly AyurvedaDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public ProductService(AyurvedaDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductQueryParameters query)
    {
        var dbQuery = _context.Products.Include(p => p.Category).Include(p => p.Reviews).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(p => p.Title.ToLower().Contains(search) ||
                                         p.Description.ToLower().Contains(search) ||
                                         p.Brand.ToLower().Contains(search) ||
                                         p.Category.Name.ToLower().Contains(search));
        }

        if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.MinPrice.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.Price <= query.MaxPrice.Value);
        }

        if (query.MinRating.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.AverageRating >= query.MinRating.Value);
        }

        dbQuery = query.SortBy?.ToLower() switch
        {
            "price_asc" => dbQuery.OrderBy(p => p.Price),
            "price_desc" => dbQuery.OrderByDescending(p => p.Price),
            "rating" => dbQuery.OrderByDescending(p => p.AverageRating),
            "stock" => dbQuery.OrderByDescending(p => p.Stock),
            _ => dbQuery.OrderByDescending(p => p.Id)
        };

        var totalCount = await dbQuery.CountAsync();
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var items = await dbQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<ProductDto>(items, totalCount, pageNumber, pageSize, totalPages);
    }

    public async Task<List<ProductDto>> GetAllProductsAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .OrderByDescending(p => p.Id)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id);

        return product == null ? null : MapToDto(product);
    }

    public async Task<List<ProductDto>> GetProductsByCategoryAsync(int categoryId)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .Where(p => p.CategoryId == categoryId)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
    {
        string imageUrl = request.ImageUrl ?? string.Empty;
        if (request.Image != null)
        {
            imageUrl = await _fileStorage.SaveFileAsync(request.Image);
        }

        var product = new Product
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Price = request.Price,
            Stock = request.Stock,
            Brand = request.Brand.Trim(),
            CategoryId = request.Category,
            ImageUrl = imageUrl,
            AverageRating = 0.0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await contextSaveWithCategoryName(product);

        return MapToDto(product);
    }

    public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductRequest request)
    {
        var product = await _context.Products.Include(p => p.Category).Include(p => p.Reviews).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return null;

        if (!string.IsNullOrWhiteSpace(request.Title)) product.Title = request.Title.Trim();
        if (!string.IsNullOrWhiteSpace(request.Description)) product.Description = request.Description.Trim();
        if (request.Price.HasValue) product.Price = request.Price.Value;
        if (request.Stock.HasValue) product.Stock = request.Stock.Value;
        if (!string.IsNullOrWhiteSpace(request.Brand)) product.Brand = request.Brand.Trim();
        if (request.Category.HasValue && request.Category.Value > 0) product.CategoryId = request.Category.Value;

        if (request.Image != null)
        {
            var newImage = await _fileStorage.SaveFileAsync(request.Image);
            product.ImageUrl = newImage;
        }

        await _context.SaveChangesAsync();
        return MapToDto(product);
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> SaveInBulkAsync(List<CreateProductRequest> requests)
    {
        int added = 0;
        foreach (var req in requests)
        {
            var product = new Product
            {
                Title = req.Title.Trim(),
                Description = req.Description.Trim(),
                Price = req.Price,
                Stock = req.Stock,
                Brand = req.Brand.Trim(),
                CategoryId = req.Category,
                ImageUrl = req.ImageUrl ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            _context.Products.Add(product);
            added++;
        }
        await _context.SaveChangesAsync();
        return added;
    }

    public async Task RecalculateRatingAsync(int productId)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return;

        var reviews = await _context.ProductReviews.Where(r => r.ProductId == productId).ToListAsync();
        if (reviews.Count > 0)
        {
            product.AverageRating = Math.Round(reviews.Average(r => r.Rating), 1);
        }
        else
        {
            product.AverageRating = 0.0;
        }

        await _context.SaveChangesAsync();
    }

    private async Task contextSaveWithCategoryName(Product product)
    {
        await _context.SaveChangesAsync();
        await _context.Entry(product).Reference(p => p.Category).LoadAsync();
    }

    private static ProductDto MapToDto(Product p) => new(
        p.Id,
        p.Title,
        p.Description,
        p.Price,
        p.Stock,
        p.Brand,
        p.ImageUrl,
        p.CategoryId,
        p.Category?.Name,
        p.AverageRating,
        p.Reviews?.Count ?? 0,
        p.CreatedAt
    );
}
