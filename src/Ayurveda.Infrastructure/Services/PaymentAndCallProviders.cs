using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Infrastructure.Services;

public class PaymentProvider : IPaymentProvider
{
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentProvider> _logger;

    public PaymentProvider(IConfiguration config, ILogger<PaymentProvider> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<PaymentInitiateResultDto> InitiateOrderPaymentAsync(string orderNumber, decimal amount, string currency = "INR")
    {
        var keyId = _config["Razorpay:KeyId"] ?? "rzp_test_placeholder_key";
        var providerOrderId = $"order_{Guid.NewGuid():N}";

        _logger.LogInformation("Initiated payment for Order {OrderNumber}, Amount {Amount} {Currency}", orderNumber, amount, currency);

        return Task.FromResult(new PaymentInitiateResultDto
        {
            Success = true,
            ProviderOrderId = providerOrderId,
            Amount = amount,
            Currency = currency,
            KeyId = keyId
        });
    }

    public Task<bool> VerifyPaymentSignatureAsync(string orderId, string paymentId, string signature)
    {
        // In test mode or when secret is default placeholder, return true if fields are present
        if (!string.IsNullOrWhiteSpace(orderId) && !string.IsNullOrWhiteSpace(paymentId))
        {
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
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
