using Microsoft.Extensions.Logging;
using SmartHotel.Booking.Application.Interfaces;

namespace SmartHotel.Booking.Infrastructure.Services;

// TODO: integrate real payment provider SDK
public class StubPaymentGateway : IPaymentGateway
{
    private readonly ILogger<StubPaymentGateway> _logger;

    public StubPaymentGateway(ILogger<StubPaymentGateway> logger)
    {
        _logger = logger;
    }

    public Task<PaymentProcessResult> ProcessTokenizedPaymentAsync(
        string paymentToken,
        decimal amount,
        string currency,
        string bookingReference,
        CancellationToken ct = default)
    {
        // Safe logging: never logs card numbers or sensitive tokens
        _logger.LogInformation("Processing tokenized payment for booking {BookingRef}, Amount: {Amount} {Currency}",
            bookingReference, amount, currency);

        // Missing token is an explicit, conclusive problem the caller caused —
        // treat as ConfirmedDecline so the draft is reset and the guest may retry.
        if (string.IsNullOrWhiteSpace(paymentToken))
        {
            return Task.FromResult(new PaymentProcessResult(
                PaymentExecutionOutcome.ConfirmedDecline,
                string.Empty,
                "Missing payment token."));
        }

        var transactionId = $"TXN-{Guid.NewGuid():N}";
        return Task.FromResult(new PaymentProcessResult(PaymentExecutionOutcome.Success, transactionId));
    }
}
