using System.Security.Claims;
using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Controllers;

[ApiController]
public class UsersController : ControllerBase
{
    private readonly AyurvedaDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public UsersController(AyurvedaDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return (claim != null && int.TryParse(claim.Value, out int id)) ? id : 0;
    }

    [HttpGet("api/users/profile")]
    [HttpGet("user/profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        int targetId = GetCurrentUserId();
        if (targetId == 0)
        {
            return Unauthorized(new { error = "Unauthorized user." });
        }

        var user = await _context.Users.FindAsync(targetId);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Contact, user.ProfileImage, user.Role, user.CreatedAt);
        return Ok(new { user = userDto });
    }

    // Admin can view any user by ID; normal user can only view their own
    [HttpGet("api/users/{id}")]
    [HttpGet("user/data/{id}")]
    [Authorize]
    public async Task<IActionResult> GetUserById(int id)
    {
        int currentId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin && currentId != id)
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { user = "User not found" });
        }

        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Contact, user.ProfileImage, user.Role, user.CreatedAt);
        return Ok(new { user = userDto });
    }

    // Admin-only lookup by email to prevent enumeration
    [HttpGet("user/email/{email}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserByEmail(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null)
        {
            return NotFound(new { user = "User not found" });
        }

        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Contact, user.ProfileImage, user.Role, user.CreatedAt);
        return Ok(new { user = userDto });
    }

    public class UserUpdateProfileRequest
    {
        public IFormFile? Profile { get; set; }
        public string? Username { get; set; }
        public string? Contact { get; set; }
    }

    [HttpPost("user/update-profile")]
    [HttpPost("api/users/update-profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromForm] UserUpdateProfileRequest request)
    {
        // IDOR FIX: Ignore any client-provided ID, strictly derive from JWT identity (Rule c)
        int targetId = GetCurrentUserId();
        if (targetId == 0)
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        var user = await _context.Users.FindAsync(targetId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        if (request.Profile != null)
        {
            var fileName = await _fileStorage.SaveFileAsync(request.Profile);
            user.ProfileImage = fileName;
        }

        if (!string.IsNullOrWhiteSpace(request.Username)) user.Username = request.Username.Trim();
        if (!string.IsNullOrWhiteSpace(request.Contact)) user.Contact = request.Contact.Trim();

        await _context.SaveChangesAsync();
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Contact, user.ProfileImage, user.Role, user.CreatedAt);

        return Ok(new { message = "Profile updated", user = userDto });
    }

    // Rule a: User delete -> admin only
    [HttpDelete("user/user/{identifier}")]
    [HttpDelete("api/users/{identifier}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string identifier)
    {
        Models.User? user = null;
        if (int.TryParse(identifier, out int id))
        {
            user = await _context.Users.FindAsync(id);
        }
        else
        {
            var normalizedEmail = identifier.Trim().ToLowerInvariant();
            user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        }

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return Ok(new { message = "User deleted successfully" });
    }
}
