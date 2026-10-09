using Microsoft.AspNetCore.Mvc;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SearchApiController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchApiController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new SearchSuggestionResultDto());

        var result = await _searchService.GetSuggestionsAsync(q, scope: "public");
        return Ok(result);
    }
}
