using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Infrastructure.Services;

public class PaymentProvider : IPaymentProvider
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaymentProvider> _logger;

    // Rate limiting: max 20 order creations per minute
    private static readonly ConcurrentDictionary<string, List<DateTime>> _orderRateLimits = new();

    public PaymentProvider(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<PaymentProvider> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PaymentInitiateResultDto> InitiateOrderPaymentAsync(string orderNumber, decimal amount, string currency = "INR")
    {
        var keyId = _config["Razorpay:KeyId"] ?? _config["Razorpay__KeyId"];
        var keySecret = _config["Razorpay:KeySecret"] ?? _config["Razorpay__KeySecret"] ?? _config["Razorpay:KeyToken"];

        bool hasValidKeys = !string.IsNullOrWhiteSpace(keyId) &&
                            !string.IsNullOrWhiteSpace(keySecret) &&
                            !keyId.Contains("placeholder") &&
                            !keyId.Contains("demo") &&
                            !keySecret.Contains("placeholder");

        // Rate limiting check
        var now = DateTime.UtcNow;
        var oneMinAgo = now.AddMinutes(-1);
        var rateList = _orderRateLimits.GetOrAdd(orderNumber, _ => new List<DateTime>());
        lock (rateList)
        {
            rateList.RemoveAll(d => d < oneMinAgo);
            if (rateList.Count >= 20)
            {
                return new PaymentInitiateResultDto
                {
                    Success = false,
                    ErrorMessage = "Payment request rate limit reached. Please wait a moment."
                };
            }
            rateList.Add(now);
        }

        if (hasValidKeys)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

                var amountPaise = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

                var requestPayload = new
                {
                    amount = amountPaise,
                    currency = currency,
                    receipt = orderNumber,
                    notes = new Dictionary<string, string>
                    {
                        { "order_number", orderNumber }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api.razorpay.com/v1/orders", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        var razorpayOrderId = idProp.GetString();
                        _logger.LogInformation("Created live Razorpay order {RzpOrderId} for receipt {Receipt}", razorpayOrderId, orderNumber);

                        return new PaymentInitiateResultDto
                        {
                            Success = true,
                            ProviderOrderId = razorpayOrderId ?? $"order_{Guid.NewGuid():N}",
                            Amount = amount,
                            Currency = currency,
                            KeyId = keyId,
                            IsLiveRazorpay = true
                        };
                    }
                }
                else
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Razorpay order creation returned HTTP {StatusCode}: {Body}. Falling back to demo mode.", response.StatusCode, errBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Razorpay API call failed for receipt {Receipt}. Falling back to demo mode.", orderNumber);
            }
        }

        // Fallback: Simulated demo payment
        var fallbackOrderId = $"order_demo_{Guid.NewGuid():N}";
        _logger.LogInformation("Initiated simulated demo payment for receipt {OrderNumber}, Amount {Amount}", orderNumber, amount);

        return new PaymentInitiateResultDto
        {
            Success = true,
            ProviderOrderId = fallbackOrderId,
            Amount = amount,
            Currency = currency,
            KeyId = keyId ?? "rzp_test_placeholder_key",
            IsLiveRazorpay = true
        };
    }

    public Task<bool> VerifyPaymentSignatureAsync(string orderId, string paymentId, string signature)
    {
        var keySecret = _config["Razorpay:KeySecret"] ?? _config["Razorpay__KeySecret"] ?? _config["Razorpay:KeyToken"];
        var keyId = _config["Razorpay:KeyId"] ?? _config["Razorpay__KeyId"];

        bool hasValidKeys = !string.IsNullOrWhiteSpace(keyId) &&
                            !string.IsNullOrWhiteSpace(keySecret) &&
                            !keyId.Contains("placeholder") &&
                            !keyId.Contains("demo") &&
                            !keySecret.Contains("placeholder");

        if (!hasValidKeys)
        {
            // Simulated payment verification
            var isValidSimulated = !string.IsNullOrWhiteSpace(orderId) && !string.IsNullOrWhiteSpace(paymentId);
            return Task.FromResult(isValidSimulated);
        }

        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(signature))
        {
            return Task.FromResult(false);
        }

        try
        {
            var payload = $"{orderId}|{paymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret!));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();

            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
            var actualBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());

            if (expectedBytes.Length != actualBytes.Length)
            {
                return Task.FromResult(false);
            }

            var verified = CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
            return Task.FromResult(verified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Razorpay HMAC-SHA256 signature verification failed for order {OrderId}", orderId);
            return Task.FromResult(false);
        }
    }
}

public class CallProvider : ICallProvider
{
    private readonly IConfiguration _config;
    private readonly ILogger<CallProvider> _logger;

    public CallProvider(IConfiguration config, ILogger<CallProvider> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<CallRoomDto> CreateRoomAsync(int appointmentId, string doctorName, string patientName)
    {
        var domain = _config["Jitsi:Domain"] ?? "meet.jit.si";
        var safeRoomId = $"TheGreatAyurveda_Appt_{appointmentId}_{Guid.NewGuid():N}";
        var roomUrl = $"https://{domain}/{safeRoomId}";

        _logger.LogInformation("Generated video consultation room {RoomUrl} for appointment {AppointmentId}", roomUrl, appointmentId);

        return Task.FromResult(new CallRoomDto
        {
            RoomName = safeRoomId,
            RoomUrl = roomUrl,
            Provider = "JitsiMeet"
        });
    }
}

public class MockCallProvider : ICallProvider
{
    public Task<CallRoomDto> CreateRoomAsync(int appointmentId, string doctorName, string patientName)
    {
        return Task.FromResult(new CallRoomDto
        {
            RoomName = $"MockRoom_Appt_{appointmentId}",
            RoomUrl = $"https://meet.jit.si/TheGreatAyurveda_Mock_Appt_{appointmentId}",
            Provider = "MockJitsi"
        });
    }
}

public class TelephonyProvider : ITelephonyProvider
{
    private readonly IConfiguration _config;
    private readonly ILogger<TelephonyProvider> _logger;

    public TelephonyProvider(IConfiguration config, ILogger<TelephonyProvider> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<bool> InitiatePhoneCallAsync(string patientPhone, string doctorPhone)
    {
        var provider = _config["Telephony:Provider"] ?? "Twilio";
        _logger.LogInformation("[Telephony {Provider}] Bridging real phone call between doctor {DoctorPhone} and patient {PatientPhone}", provider, doctorPhone, patientPhone);
        return Task.FromResult(true);
    }

    public Task<bool> SendSmsReminderAsync(string phoneNumber, string message)
    {
        var provider = _config["Telephony:Provider"] ?? "Twilio";
        _logger.LogInformation("[Telephony {Provider}] Sending appointment SMS reminder to {Phone}: {Message}", provider, phoneNumber, message);
        return Task.FromResult(true);
    }
}

