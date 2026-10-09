using MailKit.Net.Smtp;
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
        var smtpHost = _configuration["Smtp:Host"];
        var smtpPort = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
        var smtpUser = _configuration["Smtp:Username"];
        var smtpPass = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"] ?? "noreply@thegreatayurveda.com";

        // In development or when SMTP is not configured, write to outbox folder
        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser))
        {
            var outboxDir = Path.Combine(Directory.GetCurrentDirectory(), "outbox");
            Directory.CreateDirectory(outboxDir);
            var filePath = Path.Combine(outboxDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.txt");
            var content = $"To: {toEmail}\nSubject: {subject}\nDate: {DateTime.UtcNow}\n\n{bodyHtml}";
            await File.WriteAllTextAsync(filePath, content);
            _logger.LogInformation("Email written to local outbox file: {FilePath}", filePath);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("The Great Ayurveda", fromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = bodyHtml };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass ?? string.Empty);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SMTP to {ToEmail}. Fallback to local outbox.", toEmail);
            var outboxDir = Path.Combine(Directory.GetCurrentDirectory(), "outbox");
            Directory.CreateDirectory(outboxDir);
            var filePath = Path.Combine(outboxDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.txt");
            var content = $"To: {toEmail}\nSubject: {subject}\nDate: {DateTime.UtcNow}\nError: {ex.Message}\n\n{bodyHtml}";
            await File.WriteAllTextAsync(filePath, content);
        }
    }
}
