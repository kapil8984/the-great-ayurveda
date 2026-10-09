namespace Ayurveda.API.DTOs;

public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    string? ImageUrl,
    int ProductCount,
    DateTime CreatedAt
);

public record CreateCategoryDto(
    string Name,
    string? Description,
    string? ImageUrl
);
