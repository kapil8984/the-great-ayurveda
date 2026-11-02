using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Authorize(Policy = "RequireSuperAdmin")]
public class AuditController : Controller
{
    private readonly AyurvedaDbContext _context;

    public AuditController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? entity, string? search, int page = 1)
    {
        int pageSize = 20;
        var q = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(entity))
        {
            q = q.Where(a => a.EntityName == entity);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(a => a.Action.Contains(search) || a.Details!.Contains(search) || a.UserEmail!.Contains(search));
        }

        var total = await q.CountAsync();
        var logs = await q
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SelectedEntity = entity;
        ViewBag.SearchQuery = search;
        ViewBag.TotalCount = total;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

        var entities = await _context.AuditLogs.Select(a => a.EntityName).Distinct().ToListAsync();
        ViewBag.EntityList = entities;

        return View(logs);
    }
}
