namespace Ayurveda.Application.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string bodyHtml);
}

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream stream, string originalFileName, string contentType);
    void DeleteFile(string relativeFilePath);
}

public interface ISearchService
{
    Task<SearchSuggestionResultDto> GetSuggestionsAsync(string query, string scope = "public");
}

public interface IPaymentProvider
{
    Task<PaymentInitiateResultDto> InitiateOrderPaymentAsync(string orderNumber, decimal amount, string currency = "INR");
    Task<bool> VerifyPaymentSignatureAsync(string orderId, string paymentId, string signature);
}

public interface ICallProvider
{
    Task<CallRoomDto> CreateRoomAsync(int appointmentId, string doctorName, string patientName);
}

public class SearchSuggestionResultDto
{
    public string Query { get; set; } = string.Empty;
    public string? DidYouMean { get; set; }
    public string? SynonymApplied { get; set; }
    public long LatencyMs { get; set; }
    public List<string> TrendingSearches { get; set; } = new();

    // Grouped Suggestions
    public List<SearchItemDto> Products { get; set; } = new();
    public List<SearchItemDto> Categories { get; set; } = new();
    public List<SearchItemDto> Concerns { get; set; } = new();
    public List<SearchItemDto> Herbs { get; set; } = new();
    public List<SearchItemDto> Doctors { get; set; } = new();

    // Admin Scoped Suggestions
    public List<SearchItemDto> AdminOrders { get; set; } = new();
    public List<SearchItemDto> AdminUsers { get; set; } = new();
    public List<SearchItemDto> AdminProducts { get; set; } = new();

    public int TotalSuggestions => Products.Count + Categories.Count + Concerns.Count + Herbs.Count + Doctors.Count + AdminOrders.Count + AdminUsers.Count + AdminProducts.Count;
}

public class SearchItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Badge { get; set; }
    public decimal? Price { get; set; }
    public double? Rating { get; set; }
}

public class PaymentInitiateResultDto
{
    public bool Success { get; set; }
    public string ProviderOrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string? KeyId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CallRoomDto
{
    public string RoomName { get; set; } = string.Empty;
    public string RoomUrl { get; set; } = string.Empty;
    public string Provider { get; set; } = "JitsiMeet";
}
