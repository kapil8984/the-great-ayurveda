using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Ayurveda.Application.ViewModels;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class YogaController : Controller
{
    private readonly AyurvedaDbContext _context;

    public YogaController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var poses = await _context.Yogas
            .Include(y => y.Category)
            .Select(y => new YogaCardViewModel
            {
                Id = y.Id,
                YogaName = y.YogaName,
                CategoryName = y.Category.Name,
                Description = y.Description,
                ImageUrl = y.ImageUrl,
                VideoUrl = y.VideoUrl,
                AverageRating = y.AverageRating
            })
            .ToListAsync();

        return View(poses);
    }

    public async Task<IActionResult> Details(int id)
    {
        var yoga = await _context.Yogas
            .Include(y => y.Category)
            .Include(y => y.Reviews)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(y => y.Id == id);

        if (yoga == null)
            return NotFound();

        List<string> benefits = new();
        try
        {
            benefits = JsonSerializer.Deserialize<List<string>>(yoga.BenefitsJson) ?? new();
        }
        catch { }

        var model = new YogaDetailsViewModel
        {
            Id = yoga.Id,
            YogaName = yoga.YogaName,
            CategoryName = yoga.Category.Name,
            Description = yoga.Description,
            Instructions = yoga.Instructions,
            ImageUrl = yoga.ImageUrl,
            VideoUrl = yoga.VideoUrl,
            Benefits = benefits,
            AverageRating = yoga.AverageRating,
            Reviews = yoga.Reviews.Select(r => new YogaReviewViewModel
            {
                Id = r.Id,
                UserName = r.User.FullName,
                Rating = r.Rating,
                ReviewText = r.ReviewText,
                CreatedAt = r.CreatedAt
            }).ToList()
        };

        return View(model);
    }
}
