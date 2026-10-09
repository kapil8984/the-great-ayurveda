namespace Ayurveda.API.Models;

public class PasswordResetOtp
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string OtpHashed { get; set; } = string.Empty;
    public int AttemptCount { get; set; } = 0;
    public string? CreatedFromIp { get; set; }
    public DateTime ExpiryTime { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
