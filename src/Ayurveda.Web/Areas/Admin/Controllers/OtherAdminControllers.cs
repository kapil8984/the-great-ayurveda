using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookieScheme", Policy = "RequireCatalogManagement")]
public class YogaController : Controller
{
    private readonly AyurvedaDbContext _context;

    public YogaController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var yogas = await _context.Yogas
            .Include(y => y.Category)
            .OrderBy(y => y.YogaName)
            .ToListAsync();

        return View(yogas);
    }
}
