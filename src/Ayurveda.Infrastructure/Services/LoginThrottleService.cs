using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;

namespace Ayurveda.Infrastructure.Services;

public class LoginThrottleService : ILoginThrottleService
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<LoginThrottleService> _logger;

    // Track attempts per (IP + email): List of attempt timestamps
    private static readonly ConcurrentDictionary<string, List<DateTime>> _ipEmailAttempts = new();
    private static readonly ConcurrentDictionary<string, DateTime> _ipEmailBlocks = new();

    // Track attempts per account (email only across all IPs): List of attempt timestamps
    private static readonly ConcurrentDictionary<string, List<DateTime>> _accountAttempts = new();
    private static readonly ConcurrentDictionary<string, DateTime> _accountBlocks = new();

    public LoginThrottleService(IEmailSender emailSender, ILogger<LoginThrottleService> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public (bool IsBlocked, int RemainingSeconds, string Message) CheckLoginAllowed(string ipAddress, string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedIp = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
        var ipEmailKey = $"{normalizedIp}|{normalizedEmail}";
        var now = DateTime.UtcNow;

        // 1. Check stricter per-account 60-min lock
        if (_accountBlocks.TryGetValue(normalizedEmail, out var accountBlockEnd))
        {
            if (accountBlockEnd > now)
            {
                var remaining = (int)Math.Ceiling((accountBlockEnd - now).TotalSeconds);
                return (true, remaining, $"Security Alert: Too many failed login attempts for this account. Sign-in temporarily locked. Please try again in {remaining} seconds.");
            }
            else
            {
                _accountBlocks.TryRemove(normalizedEmail, out _);
            }
        }

        // 2. Check 3-in-10-min IP+Email lock
        if (_ipEmailBlocks.TryGetValue(ipEmailKey, out var ipBlockEnd))
        {
            if (ipBlockEnd > now)
            {
                var remaining = (int)Math.Ceiling((ipBlockEnd - now).TotalSeconds);
                return (true, remaining, $"Security Alert: Too many failed login attempts. Please try again in {remaining} seconds.");
            }
            else
            {
                _ipEmailBlocks.TryRemove(ipEmailKey, out _);
            }
        }

        return (false, 0, string.Empty);
    }

    public async Task<(bool IsBlocked, int RemainingSeconds, string Message)> RecordFailedLoginAsync(string ipAddress, string email, object? user = null)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedIp = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
        var ipEmailKey = $"{normalizedIp}|{normalizedEmail}";
        var now = DateTime.UtcNow;

        // 1. Record in IP+Email bucket (window: 10 minutes)
        var tenMinutesAgo = now.AddMinutes(-10);
        var attemptsList = _ipEmailAttempts.GetOrAdd(ipEmailKey, _ => new List<DateTime>());
        lock (attemptsList)
        {
            attemptsList.RemoveAll(t => t < tenMinutesAgo);
            attemptsList.Add(now);

            // 3 wrong attempts -> block for 10 minutes
            if (attemptsList.Count >= 3)
            {
                var blockUntil = now.AddMinutes(10);
                _ipEmailBlocks[ipEmailKey] = blockUntil;
                _logger.LogWarning("Login throttle triggered for IP+Email {Key}. Blocked until {Time}", ipEmailKey, blockUntil);
            }
        }

        // 2. Record in Account bucket across all IPs (window: 1 hour)
        var oneHourAgo = now.AddHours(-1);
        var accountList = _accountAttempts.GetOrAdd(normalizedEmail, _ => new List<DateTime>());
        bool triggerAccountAlert = false;
        lock (accountList)
        {
            accountList.RemoveAll(t => t < oneHourAgo);
            accountList.Add(now);

            // 15 failures per hour -> 60 min lock
            if (accountList.Count >= 15 && !_accountBlocks.ContainsKey(normalizedEmail))
            {
                var blockUntil = now.AddMinutes(60);
                _accountBlocks[normalizedEmail] = blockUntil;
                triggerAccountAlert = true;
                _logger.LogWarning("Account lockout triggered for {Email} (15 failures across network). Blocked for 60 minutes.", normalizedEmail);
            }
        }

        // 3. Dispatch security alert email to account owner if triggerAccountAlert
        if (triggerAccountAlert && user is ApplicationUser appUser && !appUser.IsDemo)
        {
            try
            {
                var alertSubject = "Security Notice: Unsuccessful Sign-in Attempts On Your Account";
                var alertHtml = $@"
<p>Dear {appUser.FullName},</p>
<p>We detected 15 consecutive unsuccessful sign-in attempts on your account from various locations within the past hour.</p>
<p>For your protection, sign-in to your account has been temporarily locked for <strong>60 minutes</strong>.</p>
<p>If you did not initiate these attempts, we strongly recommend resetting your password immediately using the 'Forgot Password' link once the temporary lock expires.</p>
<p style=""font-size: 13px; color: #718096;"">Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>";

                await _emailSender.SendEmailAsync(normalizedEmail, alertSubject, alertHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send security lockout alert email to {Email}", normalizedEmail);
            }
        }

        return CheckLoginAllowed(ipAddress, email);
    }

    public void RecordSuccessfulLogin(string ipAddress, string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedIp = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
        var ipEmailKey = $"{normalizedIp}|{normalizedEmail}";

        _ipEmailAttempts.TryRemove(ipEmailKey, out _);
        _ipEmailBlocks.TryRemove(ipEmailKey, out _);
        _accountAttempts.TryRemove(normalizedEmail, out _);
        _accountBlocks.TryRemove(normalizedEmail, out _);
    }
}
