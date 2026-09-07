namespace SmartHotel.Booking.Application.Interfaces;

public record PayHereOrderRequest(
    string OrderId,
    decimal Amount,
    string Currency,
    string CustomerFirstName,
    string CustomerLastName,
    string CustomerEmail,
    string CustomerPhone,
    string ItemsDescription,
    string ReturnUrl,
    string CancelUrl,
    string NotifyUrl);

public record PayHereOrderResponse(
    string MerchantId,
    string OrderId,
    decimal Amount,
    string Currency,
    string Hash,
    string CheckoutUrl,
    Dictionary<string, string> FormFields);

public record PayHereWebhookPayload(
    string MerchantId,
    string OrderId,
    string PaymentId,
    string PayHereAmount,
    string PayHereCurrency,
    int StatusCode,
    string Md5Sig,
    string? StatusMessage = null,
    string? Method = null);

public interface IPayHereService
{
    PayHereOrderResponse GenerateOrderRequest(PayHereOrderRequest request);
    bool VerifyWebhookSignature(PayHereWebhookPayload payload);
}
