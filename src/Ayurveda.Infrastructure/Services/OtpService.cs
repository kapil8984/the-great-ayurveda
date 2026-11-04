using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly AyurvedaDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OtpService> _logger;

    // In-memory rate limiting structures
    private static readonly ConcurrentDictionary<string, List<DateTime>> _emailSendHistory = new();
    private static readonly ConcurrentDictionary<string, List<DateTime>> _ipSendHistory = new();
    private static readonly ConcurrentDictionary<string, DateTime> _resendCooldown = new();
    private static int _globalDailyCount = 0;
    private static DateTime _globalCountDate = DateTime.UtcNow.Date;
    private static readonly object _dailyLock = new();

    public OtpService(
        AyurvedaDbContext context,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<OtpService> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool Success, string Message, string? OtpCode)> GenerateAndSendOtpAsync(
        string email,
        string purpose,
        string ipAddress,
        string? userFullName = null)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedIp = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
        var key = $"{normalizedEmail}#{purpose}";

        // 1. Check Global Daily Cap (Email__DailyLimit, default 300)
        var dailyLimit = 300;
        var limitStr = _configuration["Email:DailyLimit"] ?? _configuration["Email__DailyLimit"];
        if (int.TryParse(limitStr, out var dl) && dl > 0)
        {
            dailyLimit = dl;
        }

        lock (_dailyLock)
        {
            if (DateTime.UtcNow.Date != _globalCountDate)
            {
                _globalCountDate = DateTime.UtcNow.Date;
                _globalDailyCount = 0;
            }
            if (_globalDailyCount >= dailyLimit)
            {
                _logger.LogWarning("Global daily email limit ({Limit}) reached. Suppressing OTP dispatch.", dailyLimit);
                return (false, "System dispatch limit reached for today. Please try again tomorrow.", null);
            }
        }

        // 2. Check 60-second Resend Cooldown
        var now = DateTime.UtcNow;
        if (_resendCooldown.TryGetValue(key, out var lastSent))
        {
            var elapsed = (now - lastSent).TotalSeconds;
            if (elapsed < 60)
            {
                var remaining = 60 - (int)elapsed;
                return (false, $"Please wait {remaining} seconds before requesting a new code.", null);
            }
        }

        // 3. Rate Limit: Max 3 per email per hour
        var oneHourAgo = now.AddHours(-1);
        var emailList = _emailSendHistory.GetOrAdd(normalizedEmail, _ => new List<DateTime>());
        lock (emailList)
        {
            emailList.RemoveAll(t => t < oneHourAgo);
            if (emailList.Count >= 3)
            {
                _logger.LogWarning("Email OTP rate limit exceeded for {Email} (3 per hour)", normalizedEmail);
                return (false, "Too many OTP requests for this email address. Please try again after 1 hour.", null);
            }
        }

        // 4. Rate Limit: Max 10 per IP per hour
        var ipList = _ipSendHistory.GetOrAdd(normalizedIp, _ => new List<DateTime>());
        lock (ipList)
        {
            ipList.RemoveAll(t => t < oneHourAgo);
            if (ipList.Count >= 10)
            {
                _logger.LogWarning("IP OTP rate limit exceeded for {IP} (10 per hour)", normalizedIp);
                return (false, "Too many requests from this network. Please try again later.", null);
            }
        }

        // 5. Invalidate any existing active OTP for this exact (email, purpose)
        var existingOtps = await _context.PasswordResetOtps
            .Where(o => o.Email == key && !o.IsUsed)
            .ToListAsync();
        foreach (var old in existingOtps)
        {
            old.IsUsed = true;
        }

        // 6. Generate cryptographically secure 6-digit OTP
        var randomNum = RandomNumberGenerator.GetInt32(100000, 1000000);
        var otpCode = randomNum.ToString("D6");
        var otpHashed = BCrypt.Net.BCrypt.HashPassword(otpCode);

        var newOtp = new PasswordResetOtp
        {
            Email = key,
            OtpHashed = otpHashed,
            ExpiryUtc = DateTime.UtcNow.AddMinutes(10),
            Attempts = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.PasswordResetOtps.Add(newOtp);
        await _context.SaveChangesAsync();

        // 7. Update rate limit history
        lock (emailList) emailList.Add(now);
        lock (ipList) ipList.Add(now);
        _resendCooldown[key] = now;
        lock (_dailyLock) _globalDailyCount++;

        // 8. Prepare branded HTML & dispatch via IEmailSender
        var greeting = string.IsNullOrWhiteSpace(userFullName) ? "Dear Customer" : $"Dear {userFullName}";
        var subject = purpose switch
        {
            "ResetPassword" => "The Great Ayurveda - Password Reset Code",
            "Registration" => "The Great Ayurveda - Account Registration Code",
            _ => "The Great Ayurveda - Verification Code"
        };

        var actionText = purpose switch
        {
            "ResetPassword" => "You requested to reset your account password. Enter the 6-digit verification code below to establish a new password:",
            "Registration" => "Thank you for registering with The Great Ayurveda. Please enter the 6-digit verification code below to verify your email and activate your account:",
            _ => "Please enter the 6-digit verification code below to verify your email address:"
        };

        var bodyHtml = $@"
<p>{greeting},</p>
<p>{actionText}</p>
<div style=""text-align: center; margin: 24px 0;"">
  <div class=""otp-badge"">{otpCode}</div>
</div>
<p style=""font-size: 13px; color: #718096;"">
  This code is valid for <strong>10 minutes</strong> and can only be used once. For your security, never share this code with anyone.
</p>
<p style=""font-size: 13px; color: #718096;"">
  If you did not initiate this request, please contact our customer care immediately at care@thegreatayurveda.com.
</p>";

        var otpMode = _configuration["Auth:OtpMode"] ?? _configuration["Auth__OtpMode"] ?? "Email";
        bool isEmail = string.Equals(otpMode, "Email", StringComparison.OrdinalIgnoreCase) || string.Equals(otpMode, "Live", StringComparison.OrdinalIgnoreCase);
        bool isDemo = !isEmail;

        var smtpHost = _configuration["Email:Host"] ?? _configuration["Smtp:Host"];
        bool hasSmtp = !string.IsNullOrWhiteSpace(smtpHost) && !smtpHost.Contains("example.com");

        if (!isDemo || hasSmtp || purpose == "Registration")
        {
            try
            {
                await _emailSender.SendEmailAsync(normalizedEmail, subject, bodyHtml);
                _logger.LogInformation("Dispatched OTP code for purpose {Purpose} to {Email}", purpose, normalizedEmail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Email dispatch failed for {Email}: {Message}", normalizedEmail, ex.Message);
            }
        }

        if (!isDemo || purpose == "Registration")
        {
            return (true, "A 6-digit verification code has been dispatched to your email address.", otpCode);
        }
        else
        {
            _logger.LogInformation("Demo OTP mode active: code {Code} generated for {Purpose} to {Email}", otpCode, purpose, normalizedEmail);
            return (true, "Demo mode: enter any 6 digits to verify.", otpCode);
        }
    }

    public async Task<(bool Success, string Message)> VerifyOtpAsync(string email, string purpose, string inputOtp)
    {
        if (string.IsNullOrWhiteSpace(inputOtp) || inputOtp.Trim().Length != 6 || !inputOtp.Trim().All(char.IsDigit))
        {
            return (false, "Please enter a valid 6-digit numeric verification code.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var key = $"{normalizedEmail}#{purpose}";
        var code = inputOtp.Trim();

        var otpMode = _configuration["Auth:OtpMode"] ?? _configuration["Auth__OtpMode"] ?? "Email";
        bool isEmail = string.Equals(otpMode, "Email", StringComparison.OrdinalIgnoreCase) || string.Equals(otpMode, "Live", StringComparison.OrdinalIgnoreCase);
        bool isDemo = !isEmail;

        if (isDemo && purpose != "Registration")
        {
            // Safety: real admin/staff/seeded/protected accounts can never be verified or reset through demo OTP
            var protectedEmails = new[]
            {
                "admin@thegreatayurveda.com",
                "admin@ayurveda.com",
                "catalog@thegreatayurveda.com",
                "support@thegreatayurveda.com",
                "finance@thegreatayurveda.com",
                "customer@ayurveda.com",
                "anand@thegreatayurveda.com",
                "demo.doctor@thegreatayurveda.com",
                "demo-admin@ayurveda.local"
            };

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);
            bool isProtected = protectedEmails.Contains(normalizedEmail);
            if (!isProtected && user != null)
            {
                var roleIds = await _context.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToListAsync();
                var protectedRoleIds = await _context.Roles
                    .Where(r => r.Name == "SuperAdmin" || r.Name == "Admin" || r.Name == "CatalogManager" || r.Name == "FinanceManager" || r.Name == "SupportAgent")
                    .Select(r => r.Id)
                    .ToListAsync();
                if (roleIds.Any(id => protectedRoleIds.Contains(id)))
                {
                    isProtected = true;
                }
            }

            // Seeded doctors (ID <= 24) are protected; newly registered accounts are not seeded
            if (!isProtected && await _context.Doctors.AnyAsync(d => d.Email != null && d.Email.ToLower() == normalizedEmail && d.Id <= 24))
            {
                isProtected = true;
            }

            if (isProtected)
            {
                _logger.LogWarning("Safety block: Attempt to use demo OTP to verify or reset protected account {Email}", normalizedEmail);
                return (false, "Verification failed. Protected administrative, staff, and seeded accounts cannot be verified or reset via demo mode.");
            }

            var demoRecord = await _context.PasswordResetOtps
                .Where(o => o.Email == key && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (demoRecord != null)
            {
                demoRecord.IsUsed = true;
                await _context.SaveChangesAsync();
            }

            return (true, "Code verified successfully.");
        }

        var record = await _context.PasswordResetOtps
            .Where(o => o.Email == key && !o.IsUsed && o.ExpiryUtc > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (record == null)
        {
            return (false, "Verification code has expired or is invalid. Please request a new code.");
        }

        if (record.Attempts >= 5)
        {
            record.IsUsed = true;
            await _context.SaveChangesAsync();
            return (false, "Verification code invalidated due to exceeding 5 attempts. Please request a new code.");
        }

        record.Attempts++;

        var isCorrect = BCrypt.Net.BCrypt.Verify(code, record.OtpHashed);
        if (!isCorrect)
        {
            await _context.SaveChangesAsync();
            var remaining = 5 - record.Attempts;
            if (remaining <= 0)
            {
                record.IsUsed = true;
                await _context.SaveChangesAsync();
                return (false, "Too many incorrect attempts. This code is now invalid. Please request a new code.");
            }
            return (false, $"Invalid verification code. {remaining} attempt(s) remaining.");
        }

        // Mark OTP consumed
        record.IsUsed = true;
        await _context.SaveChangesAsync();

        return (true, "Code verified successfully.");
    }

    public Task<int> GetResendCooldownSecondsAsync(string email, string purpose)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var key = $"{normalizedEmail}#{purpose}";

        if (_resendCooldown.TryGetValue(key, out var lastSent))
        {
            var elapsed = (DateTime.UtcNow - lastSent).TotalSeconds;
            if (elapsed < 60)
            {
                return Task.FromResult(60 - (int)elapsed);
            }
        }
        return Task.FromResult(0);
    }
}
