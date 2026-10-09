using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("product/getall")]
    [HttpGet("api/products")]
    public async Task<IActionResult> GetProducts([FromQuery] ProductQueryParameters query)
    {
        if (!string.IsNullOrEmpty(query.Search) || query.CategoryId.HasValue || query.MinPrice.HasValue || query.MaxPrice.HasValue || query.MinRating.HasValue || !string.IsNullOrEmpty(query.SortBy))
        {
            var paged = await _productService.GetProductsAsync(query);
            return Ok(new { product = paged.Items, totalCount = paged.TotalCount, totalPages = paged.TotalPages, pageNumber = paged.PageNumber });
        }

        var products = await _productService.GetAllProductsAsync();
        return Ok(new { product = products });
    }

    [HttpGet("product/getbyid/{id}")]
    [HttpGet("api/products/{id}")]
    public async Task<IActionResult> GetProductById(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound(new { message = "Product not found" });
        }
        return Ok(new { Product = product, data = product });
    }

    [HttpGet("product/by-category/{categoryId}")]
    [HttpGet("api/products/category/{categoryId}")]
    public async Task<IActionResult> GetByCategory(int categoryId)
    {
        var result = await _productService.GetProductsByCategoryAsync(categoryId);
        return Ok(new { Result = result, product = result });
    }

    [HttpPost("product/addone")]
    [HttpPost("api/products")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddProduct([FromForm] CreateProductRequest request)
    {
        var created = await _productService.CreateProductAsync(request);
        return StatusCode(201, new { message = "Product added successfully", product = created });
    }

    [HttpPut("api/products/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateProduct(int id, [FromForm] UpdateProductRequest request)
    {
        var updated = await _productService.UpdateProductAsync(id, request);
        if (updated == null) return NotFound(new { message = "Product not found" });
        return Ok(new { message = "Product updated successfully", product = updated });
    }

    [HttpDelete("product/delete/{id}")]
    [HttpDelete("api/products/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var success = await _productService.DeleteProductAsync(id);
        if (!success)
        {
            return NotFound(new { error = "Bad request(id not found)", message = "Product not found" });
        }
        return Ok(new { message = "Product removed" });
    }

    [HttpPost("product/save-in-bulk")]
    [HttpPost("api/products/bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveInBulk([FromBody] List<CreateProductRequest> requests)
    {
        var count = await _productService.SaveInBulkAsync(requests);
        return Ok(new { message = $"{count} products saved successfully", count });
    }
}
