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
            .OrderBy(y => y.Id)
            .ToListAsync();

        var viewModels = poses.Select(y =>
        {
            var (meta, benefits, steps) = ParseYogaData(y.BenefitsJson, y.Instructions);

            string videoId = meta.VideoId;
            if (string.IsNullOrEmpty(videoId) && !string.IsNullOrEmpty(y.VideoUrl))
            {
                var match = System.Text.RegularExpressions.Regex.Match(y.VideoUrl, @"(?:embed\/|v=)([a-zA-Z0-9_-]{11})");
                if (match.Success) videoId = match.Groups[1].Value;
            }

            string sanskrit = !string.IsNullOrEmpty(meta.SanskritName) ? meta.SanskritName : y.YogaName.Split('(')[0].Trim();
            string english = !string.IsNullOrEmpty(meta.EnglishName) ? meta.EnglishName : 
                (y.YogaName.Contains('(') ? y.YogaName.Split('(')[1].Replace(")", "").Trim() : y.YogaName);

            return new YogaCardViewModel
            {
                Id = y.Id,
                YogaName = y.YogaName,
                SanskritName = sanskrit,
                EnglishName = english,
                CategoryName = y.Category?.Name ?? "Classical Yoga",
                Description = y.Description,
                ImageUrl = y.ImageUrl,
                VideoUrl = !string.IsNullOrEmpty(videoId) ? $"https://www.youtube-nocookie.com/embed/{videoId}" : y.VideoUrl,
                VideoId = videoId,
                ChannelTitle = !string.IsNullOrEmpty(meta.ChannelTitle) ? meta.ChannelTitle : "Ministry of Ayush",
                Difficulty = !string.IsNullOrEmpty(meta.Difficulty) ? meta.Difficulty : "Beginner",
                Duration = !string.IsNullOrEmpty(meta.Duration) ? meta.Duration : "5-10 mins",
                Benefits = benefits,
                Cautions = meta.Cautions,
                Steps = steps,
                AverageRating = y.AverageRating
            };
        }).ToList();

        return View(viewModels);
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

        var (meta, benefits, steps) = ParseYogaData(yoga.BenefitsJson, yoga.Instructions);

        string videoId = meta.VideoId;
        if (string.IsNullOrEmpty(videoId) && !string.IsNullOrEmpty(yoga.VideoUrl))
        {
            var match = System.Text.RegularExpressions.Regex.Match(yoga.VideoUrl, @"(?:embed\/|v=)([a-zA-Z0-9_-]{11})");
            if (match.Success) videoId = match.Groups[1].Value;
        }

        string sanskrit = !string.IsNullOrEmpty(meta.SanskritName) ? meta.SanskritName : yoga.YogaName.Split('(')[0].Trim();
        string english = !string.IsNullOrEmpty(meta.EnglishName) ? meta.EnglishName : 
            (yoga.YogaName.Contains('(') ? yoga.YogaName.Split('(')[1].Replace(")", "").Trim() : yoga.YogaName);

        var model = new YogaDetailsViewModel
        {
            Id = yoga.Id,
            YogaName = yoga.YogaName,
            SanskritName = sanskrit,
            EnglishName = english,
            CategoryName = yoga.Category?.Name ?? "Classical Yoga",
            Description = yoga.Description,
            Instructions = yoga.Instructions,
            ImageUrl = yoga.ImageUrl,
            VideoUrl = !string.IsNullOrEmpty(videoId) ? $"https://www.youtube-nocookie.com/embed/{videoId}" : yoga.VideoUrl,
            VideoId = videoId,
            ChannelTitle = !string.IsNullOrEmpty(meta.ChannelTitle) ? meta.ChannelTitle : "Ministry of Ayush",
            Difficulty = !string.IsNullOrEmpty(meta.Difficulty) ? meta.Difficulty : "Beginner",
            Duration = !string.IsNullOrEmpty(meta.Duration) ? meta.Duration : "5-10 mins",
            Benefits = benefits,
            Precautions = !string.IsNullOrEmpty(meta.Cautions) ? meta.Cautions : "Practice on an empty stomach. If experiencing spinal disc or cardiac conditions, consult an expert instructor before performing.",
            Steps = steps,
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

    private static (YogaMetaJson meta, List<string> benefits, List<YogaStepItem> steps) ParseYogaData(string? benefitsJson, string? instructionsJson)
    {
        var meta = new YogaMetaJson();
        var benefits = new List<string>();
        var steps = new List<YogaStepItem>();

        if (!string.IsNullOrWhiteSpace(benefitsJson))
        {
            try
            {
                var parsedMeta = JsonSerializer.Deserialize<YogaMetaJson>(benefitsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsedMeta != null && (!string.IsNullOrEmpty(parsedMeta.SanskritName) || !string.IsNullOrEmpty(parsedMeta.EnglishName) || parsedMeta.Benefits.Any()))
                {
                    meta = parsedMeta;
                    benefits = parsedMeta.Benefits;
                }
                else
                {
                    benefits = JsonSerializer.Deserialize<List<string>>(benefitsJson) ?? new();
                }
            }
            catch
            {
                try
                {
                    benefits = JsonSerializer.Deserialize<List<string>>(benefitsJson) ?? new();
                }
                catch { }
            }
        }

        if (!string.IsNullOrWhiteSpace(instructionsJson))
        {
            try
            {
                steps = JsonSerializer.Deserialize<List<YogaStepItem>>(instructionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch { }

            if (!steps.Any())
            {
                var lines = instructionsJson.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                int sNum = 1;
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        steps.Add(new YogaStepItem
                        {
                            StepNumber = sNum++,
                            Title = $"Step {sNum - 1}",
                            Instruction = trimmed,
                            ImageUrl = ""
                        });
                    }
                }
            }
        }

        return (meta, benefits, steps);
    }
}
