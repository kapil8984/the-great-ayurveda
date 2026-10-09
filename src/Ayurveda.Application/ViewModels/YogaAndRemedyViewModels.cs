namespace Ayurveda.Application.ViewModels;

public class YogaCardViewModel
{
    public int Id { get; set; }
    public string YogaName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public List<string> Benefits { get; set; } = new();
    public double AverageRating { get; set; }
}

public class YogaDetailsViewModel
{
    public int Id { get; set; }
    public string YogaName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public List<string> Benefits { get; set; } = new();
    public string? Precautions { get; set; }
    public double AverageRating { get; set; }
    public List<YogaReviewViewModel> Reviews { get; set; } = new();
}

public class YogaReviewViewModel
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DiseaseSearchViewModel
{
    public string? Query { get; set; }
    public List<HomeRemedyItemViewModel> Remedies { get; set; } = new();
    public List<ProductCardViewModel> RecommendedProducts { get; set; } = new();
    public List<DoctorCardViewModel> RecommendedDoctors { get; set; } = new();
}

public class HomeRemedyItemViewModel
{
    public string ConcernTitle { get; set; } = string.Empty;
    public string SanskritName { get; set; } = string.Empty;
    public string TraditionalDescription { get; set; } = string.Empty;
    public List<string> TraditionalRemedies { get; set; } = new();
    public List<string> DietaryAdvice { get; set; } = new();
    public List<string> SuitableHerbs { get; set; } = new();
}
