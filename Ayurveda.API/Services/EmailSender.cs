using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ayurveda.API.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        var smtpHost = _configuration["Smtp:Host"];
        var smtpPortStr = _configuration["Smtp:Port"];
        var smtpUser = _configuration["Smtp:Username"];
        var smtpPass = _configuration["Smtp:Password"];
        var fromAddress = _configuration["Smtp:From"] ?? "noreply@thegreatayurveda.com";
        var fromName = _configuration["Smtp:FromName"] ?? "The Great Ayurveda";

        // In Development or if SMTP is not configured, write to outbox folder
        if (_environment.IsDevelopment() || string.IsNullOrWhiteSpace(smtpHost))
        {
            await WriteToOutboxAsync(to, subject, body, isHtml);
            return;
        }

        int smtpPort = int.TryParse(smtpPortStr, out var p) ? p : 587;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder();
        if (isHtml)
        {
            bodyBuilder.HtmlBody = body;
        }
        else
        {
            bodyBuilder.TextBody = body;
        }
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTlsWhenAvailable);
            if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPass))
            {
                await client.AuthenticateAsync(smtpUser, smtpPass);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation("Email sent successfully to {To} with subject '{Subject}'", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} via SMTP: {Message}", to, ex.Message);
            throw;
        }
    }

    private async Task WriteToOutboxAsync(string to, string subject, string body, bool isHtml)
    {
        var outboxDir = Path.Combine(_environment.ContentRootPath, "outbox");
        if (!Directory.Exists(outboxDir))
        {
            Directory.CreateDirectory(outboxDir);
        }

        var safeTo = Path.GetInvalidFileNameChars().Aggregate(to, (current, c) => current.Replace(c, '_'));
        var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}_{safeTo}.html";
        var filePath = Path.Combine(outboxDir, fileName);

        var content = $@"<!--
To: {to}
Subject: {subject}
TimestampUtc: {DateTime.UtcNow:O}
Format: {(isHtml ? "HTML" : "PlainText")}
-->
{(isHtml ? body : $"<pre>{System.Net.WebUtility.HtmlEncode(body)}</pre>")}";

        await File.WriteAllTextAsync(filePath, content);
        _logger.LogInformation("Email written to local outbox: {FilePath}", filePath);
    }
}
