using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Payments.Commands;

public record ConfirmPaymentManualRequest(
    Guid BookingId,
    string PaymentReference,
    string? Notes = null
);

public record ConfirmPaymentManualCommand(ConfirmPaymentManualRequest Request) : IRequest<Result<string>>;

public class ConfirmPaymentManualCommandHandler : IRequestHandler<ConfirmPaymentManualCommand, Result<string>>
{
    private readonly IBookingDbContext _context;

    public ConfirmPaymentManualCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(ConfirmPaymentManualCommand command, CancellationToken ct)
    {
        var req = command.Request;

        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == req.BookingId, ct);
        if (booking == null)
        {
            return Result<string>.Failure($"Booking with ID {req.BookingId} was not found.");
        }

        if (booking.Status != BookingStatus.PendingPayment)
        {
            return Result<string>.Failure($"Cannot confirm payment for booking in {booking.Status} state.");
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == booking.Id, ct);

        var paymentRef = string.IsNullOrWhiteSpace(req.PaymentReference)
            ? $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : req.PaymentReference.Trim();

        var orderId = booking.BookingReference;

        if (payment == null)
        {
            payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = booking.TotalAmount,
                Currency = "LKR",
                Provider = PaymentProvider.PayHere,
                PayHereOrderId = orderId,
                Status = PaymentStatus.Created
            };
            _context.Payments.Add(payment);
        }

        payment.MarkCompleted(paymentRef);
        booking.ConfirmPayment(paymentRef, orderId);

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
                TotalAmount = booking.TotalAmount,
                PaymentId = paymentRef,
                ConfirmedAt = DateTime.UtcNow,
                Notes = req.Notes
            })
        });

        // Add Outbox event for payment completed
        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "payment.completed",
            Content = JsonSerializer.Serialize(new
            {
                PaymentId = payment.Id,
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                Amount = booking.TotalAmount,
                Currency = "LKR",
                PayHerePaymentId = paymentRef,
                CompletedAt = DateTime.UtcNow,
                ConfirmedBy = "Admin"
            })
        });

        await _context.SaveChangesAsync(ct);

        return Result<string>.Success(paymentRef, "Payment confirmed successfully by admin.");
    }
}
