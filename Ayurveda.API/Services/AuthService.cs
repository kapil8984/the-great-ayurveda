using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Ayurveda.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterUserAsync(UserRegisterDto dto);
    Task<AuthResponseDto> LoginUserAsync(UserLoginDto dto);
    Task<AuthResponseDto> RegisterDoctorAsync(DoctorRegisterDto dto);
    Task<AuthResponseDto> LoginDoctorAsync(DoctorLoginDto dto);
    Task<AuthResponseDto> LoginAdminAsync(AdminLoginDto dto);
    Task<bool> SeedAdminAccountAsync(string email, string password, string name, string contact, bool mustChangePassword = false);
    Task<bool> ForgotPasswordAsync(string email, string? ipAddress = null);
    Task<bool> ResetPasswordAsync(string email, string otpCode, string newPassword);
    Task<bool> ChangeDoctorPasswordAsync(int doctorId, string oldPassword, string newPassword);
    string GenerateJwtToken(int id, string email, string role, string name);
}

public class AuthService : IAuthService
{
    private const int MaxFailedAccessAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AyurvedaDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AyurvedaDbContext context,
        IConfiguration configuration,
        IEmailSender emailSender,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _emailSender = emailSender;
        _logger = logger;
    }

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public async Task<AuthResponseDto> RegisterUserAsync(UserRegisterDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new AuthResponseDto(false, "Valid email address is required", null, null, null);
        }

        if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail))
        {
            return new AuthResponseDto(false, "An account with this email already exists", null, null, null);
        }

        var user = new User
        {
            Username = dto.Username.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Contact = dto.Contact.Trim(),
            Role = "User",
            AccessFailedCount = 0,
            LockoutEndUtc = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user.Id, user.Email, user.Role, user.Username);
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Contact, user.ProfileImage, user.Role, user.CreatedAt);

        return new AuthResponseDto(true, "Registration successful", token, userDto, "User");
    }

    public async Task<AuthResponseDto> LoginUserAsync(UserLoginDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            return new AuthResponseDto(false, "Invalid email or password", null, null, null);
        }

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((user.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
            return new AuthResponseDto(false, $"Account is locked due to too many failed attempts. Try again in {remainingMinutes} minutes.", null, null, null);
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxFailedAccessAttempts)
            {
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                _logger.LogWarning("User account {Email} locked due to {Count} failed login attempts", user.Email, user.AccessFailedCount);
            }
            await _context.SaveChangesAsync();
            return new AuthResponseDto(false, "Invalid email or password", null, null, null);
        }

        // Reset lockout upon successful authentication
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user.Id, user.Email, user.Role, user.Username);
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Contact, user.ProfileImage, user.Role, user.CreatedAt);

        return new AuthResponseDto(true, "Sign In successful", token, userDto, "User");
    }

    public async Task<AuthResponseDto> RegisterDoctorAsync(DoctorRegisterDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var normalizedContact = dto.ContactNumber.Trim();

        if (await _context.Doctors.AnyAsync(d => d.Email == normalizedEmail))
        {
            return new AuthResponseDto(false, "Doctor with this email already exists", null, null, null);
        }

        if (await _context.Doctors.AnyAsync(d => d.ContactNumber == normalizedContact))
        {
            return new AuthResponseDto(false, "Doctor with this Contact number already exists", null, null, null);
        }

        var doctor = new Doctor
        {
            DoctorName = dto.DoctorName.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            ContactNumber = normalizedContact,
            Role = "Doctor",
            AccessFailedCount = 0,
            LockoutEndUtc = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync();

        // Doctor is pending approval by default - cannot use full doctor privileges until approved
        var doctorDto = new DoctorDto(doctor.Id, doctor.DoctorName, doctor.Email, doctor.ContactNumber, doctor.ProfileImage, doctor.Role, doctor.CreatedAt, null);

        return new AuthResponseDto(true, "Doctor registered successfully. Account is pending administrator review.", null, doctorDto, "Doctor");
    }

    public async Task<AuthResponseDto> LoginDoctorAsync(DoctorLoginDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var doctor = await _context.Doctors
            .Include(d => d.DoctorDetail)
            .FirstOrDefaultAsync(d => d.Email == normalizedEmail);

        if (doctor == null)
        {
            return new AuthResponseDto(false, "Invalid email or password", null, null, null);
        }

        if (doctor.LockoutEndUtc.HasValue && doctor.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((doctor.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
            return new AuthResponseDto(false, $"Account is locked due to too many failed attempts. Try again in {remainingMinutes} minutes.", null, null, null);
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, doctor.PasswordHash))
        {
            doctor.AccessFailedCount++;
            if (doctor.AccessFailedCount >= MaxFailedAccessAttempts)
            {
                doctor.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                _logger.LogWarning("Doctor account {Email} locked due to failed login attempts", doctor.Email);
            }
            await _context.SaveChangesAsync();
            return new AuthResponseDto(false, "Invalid email or password", null, null, null);
        }

        // Verify doctor approval before granting access (Rule k)
        if (doctor.DoctorDetail == null || doctor.DoctorDetail.Status == 0)
        {
            return new AuthResponseDto(false, "Your account is pending administrator approval. Please wait for verification.", null, null, null);
        }

        if (doctor.DoctorDetail.Status == -1)
        {
            return new AuthResponseDto(false, "Your doctor registration request was declined. Please contact support.", null, null, null);
        }

        doctor.AccessFailedCount = 0;
        doctor.LockoutEndUtc = null;
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(doctor.Id, doctor.Email, doctor.Role, doctor.DoctorName);
        var doctorDto = new DoctorDto(doctor.Id, doctor.DoctorName, doctor.Email, doctor.ContactNumber, doctor.ProfileImage, doctor.Role, doctor.CreatedAt, null);

        return new AuthResponseDto(true, "Sign In successful", token, doctorDto, "Doctor");
    }

    public async Task<AuthResponseDto> LoginAdminAsync(AdminLoginDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == normalizedEmail);

        if (admin == null)
        {
            return new AuthResponseDto(false, "Invalid email or password", null, null, null);
        }

        if (admin.LockoutEndUtc.HasValue && admin.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((admin.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
            return new AuthResponseDto(false, $"Account is locked due to too many failed attempts. Try again in {remainingMinutes} minutes.", null, null, null);
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, admin.PasswordHash))
        {
            admin.AccessFailedCount++;
            if (admin.AccessFailedCount >= MaxFailedAccessAttempts)
            {
                admin.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                _logger.LogWarning("Admin account {Email} locked due to failed login attempts", admin.Email);
            }
            await _context.SaveChangesAsync();
            return new AuthResponseDto(false, "Invalid email or password", null, null, null);
        }

        admin.AccessFailedCount = 0;
        admin.LockoutEndUtc = null;
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(admin.Id, admin.Email, admin.Role, admin.AdminName);
        var adminDto = new AdminDto(admin.Id, admin.AdminName, admin.Email, admin.ContactNumber, admin.ProfileImage, admin.Role, admin.CreatedAt);

        return new AuthResponseDto(true, "Sign In successful", token, adminDto, "Admin");
    }

    public async Task<bool> SeedAdminAccountAsync(string email, string password, string name, string contact, bool mustChangePassword = false)
    {
        var normalizedEmail = NormalizeEmail(email);
        var existing = await _context.Admins.FirstOrDefaultAsync(a => a.Email == normalizedEmail);
        if (existing != null)
        {
            return false;
        }

        var admin = new Admin
        {
            AdminName = name.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            ContactNumber = contact.Trim(),
            Role = "Admin",
            MustChangePassword = mustChangePassword,
            AccessFailedCount = 0,
            LockoutEndUtc = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Admins.Add(admin);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Admin account seeded successfully: {Email}", normalizedEmail);
        return true;
    }

    public async Task<bool> ForgotPasswordAsync(string email, string? ipAddress = null)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail)) return true;

        // Check rate limiting: max 1 request every 60 seconds per email
        var recentOtp = await _context.PasswordResetOtps
            .Where(o => o.Email == normalizedEmail && o.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
            .AnyAsync();

        if (recentOtp)
        {
            // Always return true generically to prevent enumeration / timing attack
            return true;
        }

        var userExists = await _context.Users.AnyAsync(u => u.Email == normalizedEmail) ||
                         await _context.Doctors.AnyAsync(d => d.Email == normalizedEmail) ||
                         await _context.Admins.AnyAsync(a => a.Email == normalizedEmail);

        if (!userExists)
        {
            // Generic return prevents email enumeration
            return true;
        }

        // Cryptographically secure 6-digit OTP
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        var otpHashed = HashOtp(normalizedEmail, otpCode);

        var otp = new PasswordResetOtp
        {
            Email = normalizedEmail,
            OtpHashed = otpHashed,
            AttemptCount = 0,
            CreatedFromIp = ipAddress,
            ExpiryTime = DateTime.UtcNow.AddMinutes(10), // 10 minutes expiry
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.PasswordResetOtps.Add(otp);
        await _context.SaveChangesAsync();

        // Send real email via IEmailSender abstraction (never console)
        var emailSubject = "The Great Ayurveda - Your Password Reset OTP";
        var emailBody = $@"
<div style='font-family: Arial, sans-serif; padding: 20px; color: #1B2A22;'>
    <h2>Password Reset Request</h2>
    <p>We received a request to reset your password for your The Great Ayurveda account.</p>
    <p>Your one-time 6-digit verification code is:</p>
    <div style='background-color: #FBF6EA; border: 1px solid #1F5E3B; padding: 15px; font-size: 24px; font-weight: bold; letter-spacing: 5px; text-align: center; color: #1F5E3B; border-radius: 8px;'>
        {otpCode}
    </div>
    <p style='margin-top: 15px;'>This code will expire in <strong>10 minutes</strong>. If you did not request this reset, please ignore this email.</p>
    <hr style='border: none; border-top: 1px solid #e0e0e0; margin: 20px 0;'/>
    <p style='font-size: 12px; color: #666;'>The Great Ayurveda &bull; Authentic Ayurveda Care & Consultation</p>
</div>";

        try
        {
            await _emailSender.SendEmailAsync(normalizedEmail, emailSubject, emailBody, isHtml: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", normalizedEmail);
        }

        return true;
    }

    public async Task<bool> ResetPasswordAsync(string email, string otpCode, string newPassword)
    {
        var normalizedEmail = NormalizeEmail(email);
        var expectedHash = HashOtp(normalizedEmail, otpCode.Trim());

        var otp = await _context.PasswordResetOtps
            .Where(o => o.Email == normalizedEmail && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
        {
            return false;
        }

        if (otp.AttemptCount >= 5)
        {
            otp.IsUsed = true;
            await _context.SaveChangesAsync();
            return false;
        }

        if (otp.OtpHashed != expectedHash)
        {
            otp.AttemptCount++;
            if (otp.AttemptCount >= 5)
            {
                otp.IsUsed = true; // Invalidate after 5 failed attempts
            }
            await _context.SaveChangesAsync();
            return false;
        }

        otp.IsUsed = true;
        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user != null)
        {
            user.PasswordHash = newHash;
            user.AccessFailedCount = 0;
            user.LockoutEndUtc = null;
        }

        var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == normalizedEmail);
        if (doctor != null)
        {
            doctor.PasswordHash = newHash;
            doctor.AccessFailedCount = 0;
            doctor.LockoutEndUtc = null;
        }

        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == normalizedEmail);
        if (admin != null)
        {
            admin.PasswordHash = newHash;
            admin.MustChangePassword = false;
            admin.AccessFailedCount = 0;
            admin.LockoutEndUtc = null;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Password reset successful for {Email}", normalizedEmail);
        return true;
    }

    public async Task<bool> ChangeDoctorPasswordAsync(int doctorId, string oldPassword, string newPassword)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, doctor.PasswordHash))
        {
            return false;
        }

        doctor.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _context.SaveChangesAsync();
        return true;
    }

    public string GenerateJwtToken(int id, string email, string role, string name)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key configuration is missing.");

        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException("JWT Key must be at least 32 bytes.");
        }

        var issuer = _configuration["Jwt:Issuer"] ?? "TheGreatAyurveda";
        var audience = _configuration["Jwt:Audience"] ?? "TheGreatAyurvedaApp";

        // Access token lifetime: honour Jwt:ExpiresInMinutes (default 30 min) or Jwt:ExpiresInDays
        var expiresInMinutesStr = _configuration["Jwt:ExpiresInMinutes"];
        var expiresInDaysStr = _configuration["Jwt:ExpiresInDays"];

        TimeSpan lifetime;
        if (int.TryParse(expiresInMinutesStr, out var minutes) && minutes > 0)
        {
            lifetime = TimeSpan.FromMinutes(minutes);
        }
        else if (int.TryParse(expiresInDaysStr, out var days) && days > 0)
        {
            lifetime = TimeSpan.FromDays(days);
        }
        else
        {
            lifetime = TimeSpan.FromMinutes(30); // Default 30 minutes short-lived access token
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Name, name),
            new Claim("sub", id.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashOtp(string email, string otpCode)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{email}:{otpCode}");
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}
