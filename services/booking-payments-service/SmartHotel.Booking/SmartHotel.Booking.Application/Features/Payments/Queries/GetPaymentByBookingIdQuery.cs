using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Payments.DTOs;
using SmartHotel.Booking.Application.Interfaces;

namespace SmartHotel.Booking.Application.Features.Payments.Queries;

public record GetPaymentByBookingIdQuery(Guid BookingId) : IRequest<Result<PaymentDto>>;

public class GetPaymentByBookingIdQueryHandler : IRequestHandler<GetPaymentByBookingIdQuery, Result<PaymentDto>>
{
    private readonly IBookingDbContext _context;

    public GetPaymentByBookingIdQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaymentDto>> Handle(GetPaymentByBookingIdQuery request, CancellationToken ct)
    {
        var payment = await _context.Payments
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(p => p.BookingId == request.BookingId, ct);

        if (payment == null)
        {
            return Result<PaymentDto>.Failure($"No payment record found for booking {request.BookingId}.");
        }

        return Result<PaymentDto>.Success(new PaymentDto
        {
            Id = payment.Id,
            BookingId = payment.BookingId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Provider = payment.Provider.ToString(),
            PayHereOrderId = payment.PayHereOrderId,
            PayHerePaymentId = payment.PayHerePaymentId,
            Status = payment.Status.ToString(),
            CapturedAt = payment.CapturedAt,
            CreatedAtUtc = payment.CreatedAtUtc
        });
    }
}
