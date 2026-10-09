using System.Text.Json;
using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Services;

public interface IYogaService
{
    Task<List<YogaDto>> GetAllYogasAsync();
    Task<YogaDto?> GetYogaByIdAsync(int id);
    Task<YogaDto> CreateYogaAsync(CreateYogaRequest request);
    Task<YogaDto?> UpdateYogaAsync(int id, UpdateYogaRequest request);
    Task<bool> DeleteYogaAsync(int id);
    Task RecalculateRatingAsync(int yogaId);
}

public class YogaService : IYogaService
{
    private readonly AyurvedaDbContext _context;

    public YogaService(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<List<YogaDto>> GetAllYogasAsync()
    {
        return await _context.Yogas
            .Include(y => y.Category)
            .Include(y => y.Reviews)
            .OrderBy(y => y.Id)
            .Select(y => MapToDto(y))
            .ToListAsync();
    }

    public async Task<YogaDto?> GetYogaByIdAsync(int id)
    {
        var yoga = await _context.Yogas
            .Include(y => y.Category)
            .Include(y => y.Reviews)
            .FirstOrDefaultAsync(y => y.Id == id);

        return yoga == null ? null : MapToDto(yoga);
    }

    public async Task<YogaDto> CreateYogaAsync(CreateYogaRequest request)
    {
        var benefitsJson = JsonSerializer.Serialize(request.Benefits ?? new List<string>());

        var yoga = new Yoga
        {
            YogaName = request.YogaName.Trim(),
            Description = request.Description.Trim(),
            Instructions = request.Instructions.Trim(),
            ImageUrl = request.ImageUrl.Trim(),
            VideoUrl = request.VideoUrl.Trim(),
            CategoryId = request.CategoryId,
            BenefitsJson = benefitsJson,
            AverageRating = 0.0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Yogas.Add(yoga);
        await _context.SaveChangesAsync();
        await _context.Entry(yoga).Reference(y => y.Category).LoadAsync();

        return MapToDto(yoga);
    }

    public async Task<YogaDto?> UpdateYogaAsync(int id, UpdateYogaRequest request)
    {
        var yoga = await _context.Yogas.Include(y => y.Category).Include(y => y.Reviews).FirstOrDefaultAsync(y => y.Id == id);
        if (yoga == null) return null;

        if (!string.IsNullOrWhiteSpace(request.YogaName)) yoga.YogaName = request.YogaName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Description)) yoga.Description = request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.Instructions)) yoga.Instructions = request.Instructions.Trim();
        if (!string.IsNullOrWhiteSpace(request.ImageUrl)) yoga.ImageUrl = request.ImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.VideoUrl)) yoga.VideoUrl = request.VideoUrl.Trim();
        if (request.CategoryId.HasValue && request.CategoryId.Value > 0) yoga.CategoryId = request.CategoryId.Value;
        if (request.Benefits != null) yoga.BenefitsJson = JsonSerializer.Serialize(request.Benefits);

        await _context.SaveChangesAsync();
        return MapToDto(yoga);
    }

    public async Task<bool> DeleteYogaAsync(int id)
    {
        var yoga = await _context.Yogas.FindAsync(id);
        if (yoga == null) return false;

        _context.Yogas.Remove(yoga);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task RecalculateRatingAsync(int yogaId)
    {
        var yoga = await _context.Yogas.FindAsync(yogaId);
        if (yoga == null) return;

        var reviews = await _context.YogaReviews.Where(r => r.YogaId == yogaId).ToListAsync();
        yoga.AverageRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 0.0;

        await _context.SaveChangesAsync();
    }

    private static YogaDto MapToDto(Yoga y) => new(
        y.Id,
        y.YogaName,
        y.Description,
        y.Instructions,
        y.ImageUrl,
        y.VideoUrl,
        y.CategoryId,
        y.Category?.Name,
        DeserializeBenefits(y.BenefitsJson),
        y.AverageRating,
        y.Reviews?.Count ?? 0,
        y.CreatedAt
    );

    private static List<string> DeserializeBenefits(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
