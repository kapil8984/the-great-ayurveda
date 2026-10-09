using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet("category/get-category")]
    [HttpGet("api/categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _categoryService.GetCategoriesAsync();
        return Ok(new { categories });
    }

    [HttpGet("api/categories/{id}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var category = await _categoryService.GetCategoryByIdAsync(id);
        if (category == null) return NotFound(new { message = "Category not found" });
        return Ok(category);
    }

    [HttpPost("category/addone")]
    [HttpPost("category/add-one")]
    [HttpPost("api/categories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddCategory([FromBody] CreateCategoryDto dto)
    {
        var created = await _categoryService.CreateCategoryAsync(dto);
        return StatusCode(201, created);
    }

    [HttpPost("category/save-in-bulk")]
    [HttpPost("api/categories/bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveInBulk([FromBody] List<CreateCategoryDto> dtos)
    {
        var count = await _categoryService.SaveInBulkAsync(dtos);
        return StatusCode(201, new { message = $"{count} categories saved successfully" });
    }

    public class DeleteCategoryBody
    {
        public int? Id { get; set; }
    }

    [HttpDelete("category/delete-category/{id?}")]
    [HttpDelete("category/delete-category")]
    [HttpDelete("api/categories/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCategory(int? id, [FromBody] DeleteCategoryBody? body)
    {
        int targetId = id ?? body?.Id ?? 0;
        if (targetId == 0) return BadRequest(new { message = "Category ID required" });

        var success = await _categoryService.DeleteCategoryAsync(targetId);
        if (!success) return NotFound(new { message = "Category not found" });

        return Ok(new { message = "Category deleted" });
    }
}
