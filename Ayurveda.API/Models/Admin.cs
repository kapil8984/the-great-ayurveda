namespace Ayurveda.API.Models;

public class Admin
{
    public int Id { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string? ProfileImage { get; set; }
    public string Role { get; set; } = "Admin";
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
