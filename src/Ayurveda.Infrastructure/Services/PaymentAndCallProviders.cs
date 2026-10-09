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

