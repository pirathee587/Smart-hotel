using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Payments.Commands;

public record PayHereWebhookCommand(PayHereWebhookPayload Payload) : IRequest<Result<string>>;

public class PayHereWebhookCommandHandler : IRequestHandler<PayHereWebhookCommand, Result<string>>
{
    private readonly IBookingDbContext _context;
    private readonly IPayHereService _payHereService;

    public PayHereWebhookCommandHandler(
        IBookingDbContext context,
        IPayHereService payHereService)
    {
        _context = context;
        _payHereService = payHereService;
    }

    public async Task<Result<string>> Handle(PayHereWebhookCommand command, CancellationToken ct)
    {
        var payload = command.Payload;

        // 1. Verify signature
        if (!_payHereService.VerifyWebhookSignature(payload))
        {
            return Result<string>.Failure("Invalid PayHere signature.");
        }

        await using var transaction = await _context.BeginBookingTransactionAsync(ct);
        using var paymentLock = await _context.AcquirePaymentLockAsync(payload.OrderId, ct);

        // 2. Find Payment
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.PayHereOrderId == payload.OrderId, ct);

        // 3. Find Booking
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.BookingReference == payload.OrderId || (payment != null && b.Id == payment.BookingId), ct);

        if (payment == null && booking == null)
        {
            return Result<string>.Failure($"Neither payment nor booking found for order reference {payload.OrderId}.");
        }

        // 4. Validate Provider Amount and Currency against server-side authoritative state
        if (!decimal.TryParse(payload.PayHereAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var providerAmount))
        {
            return Result<string>.Failure("Invalid provider amount format.");
        }

        string? authoritativeBookingCurrency = null;

        if (payment is not null)
        {
            if (providerAmount != payment.Amount)
            {
                return Result<string>.Failure("Provider amount does not match the recorded payment.");
            }

            if (!string.Equals(payload.PayHereCurrency, payment.Currency, StringComparison.OrdinalIgnoreCase))
            {
                return Result<string>.Failure("Provider currency does not match the recorded payment.");
            }

            var reusedProviderReference = await _context.Payments.AsNoTracking().AnyAsync(
                p => p.Id != payment.Id && p.PayHerePaymentId == payload.PaymentId, ct);
            if (reusedProviderReference)
            {
                return Result<string>.Failure("Provider payment reference has already been used.");
            }
        }
        else if (booking is not null)
        {
            if (providerAmount != booking.TotalAmount)
            {
                return Result<string>.Failure("Provider amount does not match the booking total amount.");
            }

            if (string.IsNullOrWhiteSpace(booking.Currency))
            {
                return Result<string>.Failure("Authoritative booking currency cannot be established: Booking does not persist a Currency field.");
            }

            if (!string.Equals(payload.PayHereCurrency, booking.Currency, StringComparison.OrdinalIgnoreCase))
            {
                return Result<string>.Failure("Provider currency does not match the authoritative booking currency.");
            }

            authoritativeBookingCurrency = booking.Currency;
        }

        // 5. Process based on status_code
        // Status Codes: 2 = Success, 0 = Pending, -1 = Canceled, -2 = Failed, -3 = Chargedback
        if (payload.StatusCode == 2)
        {
            if (payment != null && payment.Status != PaymentStatus.Completed)
            {
                payment.MarkCompleted(payload.PaymentId);
            }

            if (booking != null && booking.Status == BookingStatus.PendingPayment)
            {
                booking.ConfirmPayment(payload.PaymentId, payload.OrderId);

                var authoritativeAmount = payment?.Amount ?? booking.TotalAmount;
                var authoritativeCurrency = payment?.Currency ?? authoritativeBookingCurrency!;

                // Add Outbox event for booking confirmation
                _context.OutboxMessages.Add(new OutboxMessage
                {
                    Type = "booking.confirmed",
                    Content = JsonSerializer.Serialize(new
                    {
                        BookingId = booking.Id,
                        BookingReference = booking.BookingReference,
                        CustomerId = booking.CustomerId,
                        CustomerLastName = booking.CustomerLastName,
                        CustomerEmail = booking.CustomerEmail,
                        RoomId = booking.RoomId,
                        RoomTypeId = booking.RoomTypeId,
                        CheckInDate = booking.CheckInDate,
                        CheckOutDate = booking.CheckOutDate,
                        TotalAmount = authoritativeAmount,
                        PaymentId = payload.PaymentId,
                        ConfirmedAt = DateTime.UtcNow
                    })
                });

                // Add Outbox event for payment completed
                _context.OutboxMessages.Add(new OutboxMessage
                {
                    Type = "payment.completed",
                    Content = JsonSerializer.Serialize(new
                    {
                        PaymentId = payment?.Id ?? Guid.NewGuid(),
                        BookingId = booking.Id,
                        BookingReference = booking.BookingReference,
                        Amount = authoritativeAmount,
                        Currency = authoritativeCurrency,
                        PayHerePaymentId = payload.PaymentId,
                        CompletedAt = DateTime.UtcNow
                    })
                });
            }

            await _context.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Result<string>.Success(payload.PaymentId, "Payment confirmed successfully.");
        }
        else if (payload.StatusCode < 0)
        {
            if (payment != null && payment.Status == PaymentStatus.Created)
            {
                payment.MarkFailed();
            }

            await _context.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Result<string>.Success(payload.OrderId, $"Payment marked as failed/cancelled with status code {payload.StatusCode}.");
        }

        return Result<string>.Success(payload.OrderId, $"PayHere notification received with status code {payload.StatusCode}.");
    }
}
