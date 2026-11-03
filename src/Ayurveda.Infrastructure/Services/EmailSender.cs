using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml)
    {
        // 1. Resolve configuration with defaults
        var mode = _configuration["Email:Mode"] 
            ?? _configuration["Email__Mode"] 
            ?? (_configuration.GetValue<bool>("DemoMode", false) ? "File" : "Smtp");

        var host = _configuration["Email:Host"] 
            ?? _configuration["Email__Host"] 
            ?? _configuration["Smtp:Host"] 
            ?? "smtp.gmail.com";

        var portStr = _configuration["Email:Port"] 
            ?? _configuration["Email__Port"] 
            ?? _configuration["Smtp:Port"];
        var port = int.TryParse(portStr, out var p) ? p : 587;

        var user = _configuration["Email:User"] 
            ?? _configuration["Email__User"] 
            ?? _configuration["Smtp:Username"];

        var pass = _configuration["Email:Password"] 
            ?? _configuration["Email__Password"] 
            ?? _configuration["Smtp:Password"];

        var fromName = _configuration["Email:FromName"] 
            ?? _configuration["Email__FromName"] 
            ?? "The Great Ayurveda";

        var fromEmail = _configuration["Email:FromEmail"] 
            ?? _configuration["Email__FromEmail"] 
            ?? _configuration["Smtp:FromEmail"] 
            ?? "noreply@thegreatayurveda.com";

        var enableSsl = true;
        var sslStr = _configuration["Email:EnableSsl"] ?? _configuration["Email__EnableSsl"];
        if (!string.IsNullOrEmpty(sslStr) && bool.TryParse(sslStr, out var sslVal))
        {
            enableSsl = sslVal;
        }

        // 2. Build multi-part MIME message (HTML + plain text alternative)
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;

        // Plain text fallback from HTML
        var plainText = Regex.Replace(bodyHtml, "<[^>]*>", " ")
                             .Replace("&nbsp;", " ")
                             .Replace("&amp;", "&")
                             .Trim();

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = WrapBrandedHtml(subject, bodyHtml),
            TextBody = plainText
        };
        message.Body = bodyBuilder.ToMessageBody();

        // 3. Mode: File -> Write .eml RFC822 file into App_Data/outbox
        var isFileMode = string.Equals(mode, "File", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(user) 
            || string.IsNullOrWhiteSpace(pass)
            || host.Contains("example.com", StringComparison.OrdinalIgnoreCase);

        if (isFileMode)
        {
            await WriteEmlToOutboxAsync(message, toEmail, subject);
            return;
        }

        // 4. Mode: Smtp -> Connect and send with 10s timeout
        try
        {
            using var client = new SmtpClient();
            client.Timeout = 10000; // 10 second timeout

            var socketOptions = enableSsl 
                ? (port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls) 
                : SecureSocketOptions.None;

            await client.ConnectAsync(host, port, socketOptions);
            if (!string.IsNullOrEmpty(user))
            {
                await client.AuthenticateAsync(user, pass ?? string.Empty);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email successfully dispatched via SMTP to {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            // Log without secrets and never crash; fall back to outbox
            _logger.LogWarning("SMTP dispatch failed for {ToEmail} ({Error}). Writing copy to outbox.", toEmail, ex.Message);
            await WriteEmlToOutboxAsync(message, toEmail, subject, ex.Message);
        }
    }

    private async Task WriteEmlToOutboxAsync(MimeMessage message, string toEmail, string subject, string? error = null)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var currentDir = Directory.GetCurrentDirectory();

            var outboxDirs = new[]
            {
                Path.Combine(currentDir, "App_Data", "outbox"),
                Path.Combine(currentDir, "outbox"),
                Path.Combine(baseDir, "App_Data", "outbox")
            };

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filename = $"{timestamp}_{Guid.NewGuid():N}.eml";

            foreach (var dir in outboxDirs)
            {
                Directory.CreateDirectory(dir);
                var fullPath = Path.Combine(dir, filename);
                using var stream = File.Create(fullPath);
                await message.WriteToAsync(stream);
            }

            _logger.LogInformation("Saved message .eml to outbox for {ToEmail}. Subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write .eml to outbox for {ToEmail}", toEmail);
        }
    }

    private static string WrapBrandedHtml(string subject, string innerHtml)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
<title>{subject}</title>
<style>
body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #F8F6F0; margin: 0; padding: 20px; color: #2D3748; }}
.email-container {{ max-width: 580px; margin: 0 auto; background: #FFFFFF; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 16px rgba(0,0,0,0.06); border: 1px solid #E5DED2; }}
.email-header {{ background: linear-gradient(135deg, #0E2B1B 0%, #143F27 50%, #1F5E3B 100%); padding: 24px 32px; text-align: center; color: #FFFFFF; }}
.email-title {{ color: #D4AF37; font-size: 20px; font-weight: bold; margin: 0; font-family: Georgia, serif; }}
.email-body {{ padding: 32px; font-size: 15px; line-height: 1.6; }}
.email-footer {{ background: #FAF7F2; padding: 18px 32px; text-align: center; font-size: 12px; color: #718096; border-top: 1px solid #E5DED2; }}
.otp-badge {{ display: inline-block; font-size: 28px; font-weight: bold; letter-spacing: 6px; padding: 12px 24px; background: #FAF7F2; border: 2px dashed #D4AF37; color: #0E2B1B; border-radius: 8px; margin: 16px 0; }}
</style>
</head>
<body>
<div class=""email-container"">
  <div class=""email-header"">
    <div class=""email-title"">The Great Ayurveda</div>
    <div style=""font-size: 12px; color: #F3E5AB; margin-top: 4px;"">Authentic Classical Ayurveda Care &amp; Consultations (Demo)</div>
  </div>
  <div class=""email-body"">
    {innerHtml}
  </div>
  <div class=""email-footer"">
    &copy; {DateTime.UtcNow.Year} The Great Ayurveda. This is an automated notification. Please do not reply directly.
  </div>
</div>
</body>
</html>";
    }
}
