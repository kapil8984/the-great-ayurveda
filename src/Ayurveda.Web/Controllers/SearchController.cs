using Microsoft.AspNetCore.Mvc;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Web.Controllers;

[Route("search")]
public class SearchController : Controller
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(new SearchSuggestionResultDto());

        var result = await _searchService.GetSuggestionsAsync(q, scope: "public");
        return Json(result);
    }
}
