namespace Ayurveda.Domain.Entities;

// Kept for backward compatibility with existing legacy tables during Identity migration
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string Role { get; set; } = "Customer";
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Admin
{
    public int Id { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
