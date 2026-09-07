using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Domain.Policies;

namespace SmartHotel.Booking.Application.Features.Bookings.Commands;

public record CancelBookingResponse
{
    public Guid BookingId { get; init; }
    public BookingStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal RefundAmount { get; init; }
    public decimal CancellationFee { get; init; }
    public decimal RefundPercentage { get; init; }
    public string TierDescription { get; init; } = string.Empty;
}

public record CancelBookingCommand(Guid BookingId, Guid? CustomerId = null, string? Reason = null) : IRequest<Result<CancelBookingResponse>>;

public class CancelBookingCommandHandler : IRequestHandler<CancelBookingCommand, Result<CancelBookingResponse>>
{
    private readonly IBookingDbContext _context;

    public CancelBookingCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CancelBookingResponse>> Handle(CancelBookingCommand command, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == command.BookingId, ct);

        if (booking == null)
        {
            return Result<CancelBookingResponse>.Failure($"Booking with ID {command.BookingId} was not found.");
        }

        // If CustomerId specified, ensure only owner can cancel
        if (command.CustomerId.HasValue && booking.CustomerId != command.CustomerId.Value)
        {
            return Result<CancelBookingResponse>.Failure("You are not authorized to cancel this booking.");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return Result<CancelBookingResponse>.Failure("Booking is already cancelled.");
        }

        if (booking.Status == BookingStatus.CheckedIn || booking.Status == BookingStatus.CheckedOut)
        {
            return Result<CancelBookingResponse>.Failure($"Cannot cancel a booking that is currently {booking.Status}.");
        }

        decimal refundAmount = 0;
        decimal feeAmount = 0;
        decimal refundPct = 0;
        string tierDesc = "No refund (unpaid booking).";

        // If booking was Confirmed with payment, calculate refund per approved 3-tier policy
        if (booking.Status == BookingStatus.Confirmed)
        {
            var refundResult = RefundPolicy.CalculateRefund(booking.TotalAmount, booking.CheckInDate, DateTime.UtcNow);
            refundAmount = refundResult.RefundAmount;
            feeAmount = refundResult.FeeAmount;
            refundPct = refundResult.RefundPercentage;
            tierDesc = refundResult.TierDescription;

            var completedPayment = booking.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Completed);
            if (completedPayment != null && refundAmount > 0)
            {
                completedPayment.ApplyRefund(refundAmount, command.Reason ?? tierDesc);

                // Write payment.refunded outbox message
                _context.OutboxMessages.Add(new OutboxMessage
                {
                    Type = "payment.refunded",
                    Content = JsonSerializer.Serialize(new
                    {
                        PaymentId = completedPayment.Id,
                        BookingId = booking.Id,
                        RefundAmount = refundAmount,
                        FeeAmount = feeAmount,
                        RefundPercentage = refundPct,
                        Reason = command.Reason ?? tierDesc,
                        OccurredOnUtc = DateTime.UtcNow
                    })
                });
            }
        }

        booking.Cancel();

        // Write booking.cancelled outbox message
        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "booking.cancelled",
            Content = JsonSerializer.Serialize(new
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                CustomerId = booking.CustomerId,
                RoomId = booking.RoomId,
                TotalAmount = booking.TotalAmount,
                RefundAmount = refundAmount,
                FeeAmount = feeAmount,
                OccurredOnUtc = DateTime.UtcNow
            })
        });

        await _context.SaveChangesAsync(ct);

        var response = new CancelBookingResponse
        {
            BookingId = booking.Id,
            Status = booking.Status,
            TotalAmount = booking.TotalAmount,
            RefundAmount = refundAmount,
            CancellationFee = feeAmount,
            RefundPercentage = refundPct,
            TierDescription = tierDesc
        };

        return Result<CancelBookingResponse>.Success(response, $"Booking cancelled. {tierDesc}");
    }
}
