using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

[ApiController]
[Route("api/webhook/razorpay")]
[IgnoreAntiforgeryToken]
public class PaymentWebhookController : ControllerBase
{
    private readonly AyurvedaDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(
        AyurvedaDbContext context,
        IConfiguration configuration,
        ILogger<PaymentWebhookController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest(new { message = "Empty webhook payload" });
        }

        var webhookSecret = _configuration["Razorpay:WebhookSecret"] 
            ?? _configuration["Razorpay__WebhookSecret"] 
            ?? _configuration["Razorpay:KeySecret"] 
            ?? _configuration["Razorpay__KeySecret"];

        var signatureHeader = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();

        // If webhook secret configured, verify HMAC-SHA256 signature
        if (!string.IsNullOrWhiteSpace(webhookSecret) && 
            !webhookSecret.Contains("placeholder") && 
            !webhookSecret.Contains("demo"))
        {
            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                _logger.LogWarning("Razorpay webhook rejected: missing X-Razorpay-Signature header.");
                return Unauthorized(new { message = "Missing signature header" });
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();

            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
            var actualBytes = Encoding.UTF8.GetBytes(signatureHeader.Trim().ToLowerInvariant());

            if (expectedBytes.Length != actualBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            {
                _logger.LogWarning("Razorpay webhook signature verification failed.");
                return Unauthorized(new { message = "Invalid signature" });
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("event", out var evProp) ? evProp.GetString() : null;

            _logger.LogInformation("Processing Razorpay webhook event: {EventType}", eventType);

            if (eventType == "payment.captured" || eventType == "order.paid")
            {
                string? receipt = null;
                string? paymentId = null;

                if (root.TryGetProperty("payload", out var payload))
                {
                    if (payload.TryGetProperty("order", out var orderEntity) && 
                        orderEntity.TryGetProperty("entity", out var orderObj))
                    {
                        if (orderObj.TryGetProperty("receipt", out var receiptProp))
                            receipt = receiptProp.GetString();
                    }

                    if (payload.TryGetProperty("payment", out var paymentEntity) && 
                        paymentEntity.TryGetProperty("entity", out var paymentObj))
                    {
                        if (paymentObj.TryGetProperty("id", out var pidProp))
                            paymentId = pidProp.GetString();
                        
                        if (string.IsNullOrEmpty(receipt) && paymentObj.TryGetProperty("notes", out var notes))
                        {
                            if (notes.TryGetProperty("order_number", out var onProp))
                                receipt = onProp.GetString();
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(receipt))
                {
                    var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == receipt);
                    if (order != null)
                    {
                        // Idempotency: If order is already paid, acknowledge without double mutation
                        if (order.PaymentStatus == "Paid")
                        {
                            _logger.LogInformation("Webhook idempotency: Order {OrderNumber} already marked Paid.", receipt);
                            return Ok(new { status = "acknowledged", duplicate = true });
                        }

                        order.PaymentStatus = "Paid";
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Order {OrderNumber} updated to Paid via webhook (PaymentId: {PaymentId})", receipt, paymentId);
                    }
                }
            }

            return Ok(new { status = "ok" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Razorpay webhook payload.");
            return StatusCode(500, new { message = "Error processing webhook" });
        }
    }
}

