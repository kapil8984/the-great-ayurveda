using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task<CategoryDto?> GetCategoryByIdAsync(int id);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto);
    Task<bool> DeleteCategoryAsync(int id);
    Task<int> SaveInBulkAsync(List<CreateCategoryDto> dtos);
}

public class CategoryService : ICategoryService
{
    private readonly AyurvedaDbContext _context;

    public CategoryService(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Include(c => c.Products)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.ImageUrl,
                c.Products.Count,
                c.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        return category == null ? null : new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.ImageUrl,
            category.Products.Count,
            category.CreatedAt
        );
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto)
    {
        var category = new Category
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            ImageUrl = dto.ImageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return new CategoryDto(category.Id, category.Name, category.Description, category.ImageUrl, 0, category.CreatedAt);
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return false;

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> SaveInBulkAsync(List<CreateCategoryDto> dtos)
    {
        int count = 0;
        foreach (var dto in dtos)
        {
            if (!await _context.Categories.AnyAsync(c => c.Name == dto.Name.Trim()))
            {
                _context.Categories.Add(new Category
                {
                    Name = dto.Name.Trim(),
                    Description = dto.Description?.Trim(),
                    ImageUrl = dto.ImageUrl,
                    CreatedAt = DateTime.UtcNow
                });
                count++;
            }
        }
        await _context.SaveChangesAsync();
        return count;
    }
}
