using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Infrastructure.Services;

public class SearchService : ISearchService
{
    private readonly AyurvedaDbContext _context;

    public SearchService(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<SearchSuggestionResultDto> GetSuggestionsAsync(string query, string scope = "public")
    {
        var result = new SearchSuggestionResultDto { Query = query };

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return result;

        var term = query.Trim().ToLower();

        if (scope.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            // Admin search scope: Orders, Users, Doctors, Products
            result.AdminOrders = await _context.Orders
                .Where(o => (o.OrderNumber != null && o.OrderNumber.ToLower().Contains(term)) || (o.User != null && o.User.Email != null && o.User.Email.ToLower().Contains(term)))
                .Take(5)
                .Select(o => new SearchItemDto
                {
                    Id = o.Id.ToString(),
                    Title = $"Order #{o.OrderNumber}",
                    Subtitle = $"Total: ₹{o.TotalAmount:N2} | Status: {o.Status}",
                    Url = $"/Admin/Orders/Details/{o.Id}",
                    Badge = o.Status
                })
                .ToListAsync();

            result.AdminUsers = await _context.Users
                .Where(u => (u.Email != null && u.Email.ToLower().Contains(term)) || (u.FullName != null && u.FullName.ToLower().Contains(term)))
                .Take(5)
                .Select(u => new SearchItemDto
                {
                    Id = u.Id.ToString(),
                    Title = u.FullName,
                    Subtitle = u.Email ?? string.Empty,
                    Url = $"/Admin/Users/Details/{u.Id}",
                    Badge = "User"
                })
                .ToListAsync();

            result.Doctors = await _context.Doctors
                .Where(d => d.DoctorName.ToLower().Contains(term) || d.Email.ToLower().Contains(term))
                .Take(5)
                .Select(d => new SearchItemDto
                {
                    Id = d.Id.ToString(),
                    Title = $"Dr. {d.DoctorName}",
                    Subtitle = d.Email,
                    ImageUrl = d.ProfileImage,
                    Url = $"/Admin/Doctors/Details/{d.Id}",
                    Badge = d.DoctorDetail != null && d.DoctorDetail.Status == 1 ? "Approved" : "Pending"
                })
                .ToListAsync();

            result.Products = await _context.Products
                .Where(p => p.Title.ToLower().Contains(term) || p.Brand.ToLower().Contains(term))
                .Take(5)
                .Select(p => new SearchItemDto
                {
                    Id = p.Id.ToString(),
                    Title = p.Title,
                    Subtitle = $"{p.Brand} | ₹{p.Price:N2}",
                    ImageUrl = p.ImageUrl,
                    Url = $"/Admin/Products/Edit/{p.Id}",
                    Badge = p.Stock > 0 ? "In Stock" : "Out of Stock"
                })
                .ToListAsync();

            return result;
        }

        // Public storefront scope: Products, Categories, Concerns/Herbs, Doctors
        result.Products = await _context.Products
            .Where(p => p.IsActive && (p.Title.ToLower().Contains(term) || p.Brand.ToLower().Contains(term) || (p.Tags != null && p.Tags.ToLower().Contains(term))))
            .Take(6)
            .Select(p => new SearchItemDto
            {
                Id = p.Id.ToString(),
                Title = p.Title,
                Subtitle = $"by {p.Brand} • ₹{p.Price:N2}",
                ImageUrl = p.ImageUrl,
                Url = $"/Products/Details/{p.Id}",
                Badge = p.Stock > 0 ? "₹" + p.Price : "Out of Stock"
            })
            .ToListAsync();

        result.Categories = await _context.Categories
            .Where(c => c.IsActive && c.Name.ToLower().Contains(term))
            .Take(4)
            .Select(c => new SearchItemDto
            {
                Id = c.Id.ToString(),
                Title = c.Name,
                Subtitle = "Category",
                ImageUrl = c.ImageUrl,
                Url = $"/Shop?categoryId={c.Id}",
                Badge = "Category"
            })
            .ToListAsync();

        result.Doctors = await _context.Doctors
            .Where(d => d.DoctorDetail != null && d.DoctorDetail.Status == 1 &&
                       (d.DoctorName.ToLower().Contains(term) || (d.DoctorDetail.Specialization != null && d.DoctorDetail.Specialization.Name.ToLower().Contains(term))))
            .Take(4)
            .Select(d => new SearchItemDto
            {
                Id = d.Id.ToString(),
                Title = $"Dr. {d.DoctorName}",
                Subtitle = d.DoctorDetail != null && d.DoctorDetail.Specialization != null ? d.DoctorDetail.Specialization.Name : "Ayurvedic Practitioner",
                ImageUrl = d.ProfileImage,
                Url = $"/Doctors/Profile/{d.Id}",
                Badge = "Verified Doctor"
            })
            .ToListAsync();

        // Concern / Disease matching
        var concerns = new List<(string Name, string Description)>
        {
            ("Digestion & Gut Health", "Ayurvedic formulations for healthy metabolism"),
            ("Immunity & Rasayana", "Traditional herbs to bolster natural vitality"),
            ("Joint & Muscle Comfort", "Herbal oils and soothing formulations"),
            ("Stress & Restful Sleep", "Ashwagandha, Brahmi and calming botanicals"),
            ("Skin & Complexion", "Pure herbs for natural radiance and balance"),
            ("Hair Care & Scalp", "Bhringraj, Amla and nourishing herbal oils")
        };

        result.Concerns = concerns
            .Where(c => c.Name.ToLower().Contains(term) || c.Description.ToLower().Contains(term))
            .Take(3)
            .Select(c => new SearchItemDto
            {
                Title = c.Name,
                Subtitle = c.Description,
                Url = $"/Diseases?query={Uri.EscapeDataString(c.Name)}",
                Badge = "Concern"
            })
            .ToList();

        return result;
    }
}
