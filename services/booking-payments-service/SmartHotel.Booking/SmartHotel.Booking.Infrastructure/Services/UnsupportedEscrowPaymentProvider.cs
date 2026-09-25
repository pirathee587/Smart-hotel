using Microsoft.Extensions.Logging;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Infrastructure.Services;

/// <summary>Fail-closed adapter used until a contracted escrow provider is configured.</summary>
public sealed class UnsupportedEscrowPaymentProvider : IEscrowPaymentProvider
{
    private readonly ILogger<UnsupportedEscrowPaymentProvider> _logger;
    public UnsupportedEscrowPaymentProvider(ILogger<UnsupportedEscrowPaymentProvider> logger) => _logger = logger;
    public PaymentProvider Provider => PaymentProvider.Escrow;
    public EscrowProviderCapabilities Capabilities => new(false, false, false, false);

    public Task<EscrowCheckoutResult> CreateCheckoutAsync(string bookingReference, decimal amount, string currency, string idempotencyKey, CancellationToken ct)
        => Task.FromResult(new EscrowCheckoutResult(false, "", "", "", "No escrow-capable provider is configured."));

    public Task<EscrowOperationResult> ReleaseAsync(string providerPaymentReference, decimal amount, string currency, string idempotencyKey, CancellationToken ct)
    {
        _logger.LogWarning("Escrow release refused because no escrow-capable provider is configured.");
        return Task.FromResult(new EscrowOperationResult(false, false, "", "No escrow-capable provider is configured."));
    }

    public Task<EscrowOperationResult> RefundAsync(string providerPaymentReference, decimal amount, string currency, string idempotencyKey, CancellationToken ct)
        => Task.FromResult(new EscrowOperationResult(false, false, "", "No escrow-capable provider is configured."));

    public bool TryVerifyWebhook(string rawPayload, string signature, out EscrowWebhookEvent? webhookEvent)
    {
        webhookEvent = null;
        return false;
    }
}
