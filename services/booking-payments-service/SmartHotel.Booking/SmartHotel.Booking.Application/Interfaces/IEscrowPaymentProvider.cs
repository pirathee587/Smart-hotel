using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Interfaces;

public record EscrowProviderCapabilities(bool CanHoldFunds, bool CanReleaseFunds, bool CanRefund, bool CanReportSettlement);
public record EscrowOperationResult(bool Succeeded, bool Verified, string ProviderReference, string? Error = null);
public record EscrowCheckoutResult(bool Succeeded, string CheckoutUrl, string PaymentReference, string CheckoutReference, string? Error = null);
public record EscrowWebhookEvent(string EventId, string EventType, string PaymentReference, string OperationReference, decimal Amount, string Currency);

public interface IEscrowPaymentProvider
{
    PaymentProvider Provider { get; }
    EscrowProviderCapabilities Capabilities { get; }
    Task<EscrowCheckoutResult> CreateCheckoutAsync(string bookingReference, decimal amount, string currency, string idempotencyKey, CancellationToken ct);
    Task<EscrowOperationResult> ReleaseAsync(string providerPaymentReference, decimal amount, string currency, string idempotencyKey, CancellationToken ct);
    Task<EscrowOperationResult> RefundAsync(string providerPaymentReference, decimal amount, string currency, string idempotencyKey, CancellationToken ct);
    bool TryVerifyWebhook(string rawPayload, string signature, out EscrowWebhookEvent? webhookEvent);
}
