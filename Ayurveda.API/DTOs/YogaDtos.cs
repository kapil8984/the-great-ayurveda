namespace Ayurveda.API.DTOs;

public record YogaDto(
    int Id,
    string YogaName,
    string Description,
    string Instructions,
    string ImageUrl,
    string VideoUrl,
    int CategoryId,
    string? CategoryName,
    List<string> Benefits,
    double AverageRating,
    int ReviewCount,
    DateTime CreatedAt
);

public class CreateYogaRequest
{
    public string YogaName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public List<string>? Benefits { get; set; }
}

public class UpdateYogaRequest
{
    public string? YogaName { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public int? CategoryId { get; set; }
    public List<string>? Benefits { get; set; }
}
