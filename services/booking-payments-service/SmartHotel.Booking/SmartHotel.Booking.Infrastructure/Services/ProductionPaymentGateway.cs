using Microsoft.Extensions.Logging;
using SmartHotel.Booking.Application.Interfaces;

namespace SmartHotel.Booking.Infrastructure.Services;

public class ProductionPaymentGateway : IPaymentGateway
{
    private readonly ILogger<ProductionPaymentGateway> _logger;

    public ProductionPaymentGateway(ILogger<ProductionPaymentGateway> logger)
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
        // No production SDK is wired up. We cannot conclusively identify a
        // card decline without an authoritative provider response, so we
        // return Unknown rather than ConfirmedDecline to prevent incorrectly
        // releasing a potentially-paid reservation.
        _logger.LogCritical(
            "Production payment gateway invoked for booking {BookingRef}, but no production payment provider SDK is configured. Failing closed as Unknown.",
            bookingReference);

        return Task.FromResult(new PaymentProcessResult(
            PaymentExecutionOutcome.Unknown,
            string.Empty,
            "Payment processing is unavailable: no production payment gateway configured."));
    }
}
