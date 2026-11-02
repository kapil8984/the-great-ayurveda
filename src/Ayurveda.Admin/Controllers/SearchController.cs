using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Admin.Controllers;

[Authorize(Roles = "SuperAdmin,CatalogManager,SupportAgent,FinanceManager")]
[Route("search")]
public class SearchController : Controller
{
    private readonly ISearchService _searchService;
    private readonly IMemoryCache _cache;

    public SearchController(ISearchService searchService, IMemoryCache cache)
    {
        _searchService = searchService;
        _cache = cache;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, [FromQuery] string scope = "admin")
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(new SearchSuggestionResultDto());
        }

        var result = await _searchService.GetSuggestionsAsync(q.Trim(), scope: "admin");
        Response.Headers.Append("X-Search-Latency-Ms", result.LatencyMs.ToString());

        return Json(result);
    }
}
