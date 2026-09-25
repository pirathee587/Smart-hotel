using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Kiosk.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Kiosk.Queries;

public record KioskLookupBookingQuery(string BookingReference, string LastName) : IRequest<Result<KioskBookingDto>>;

public class KioskLookupBookingQueryHandler : IRequestHandler<KioskLookupBookingQuery, Result<KioskBookingDto>>
{
    private readonly IBookingDbContext _context;

    public KioskLookupBookingQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<KioskBookingDto>> Handle(KioskLookupBookingQuery query, CancellationToken ct)
    {
        var refNormalized = query.BookingReference.Trim().ToUpperInvariant();
        var nameNormalized = query.LastName.Trim().ToLowerInvariant();

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.BookingReference.ToUpper() == refNormalized &&
                                      (b.CustomerLastName.ToLower() == nameNormalized ||
                                       b.CustomerEmail.ToLower() == nameNormalized), ct);

        if (booking == null)
        {
            return Result<KioskBookingDto>.Failure("No reservation found matching the provided reference and name/email.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var canCheckIn = booking.Status == BookingStatus.Confirmed && today >= booking.CheckInDate && today <= booking.CheckOutDate;
        var canCheckOut = booking.Status == BookingStatus.CheckedIn;

        var dto = new KioskBookingDto
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerLastName = booking.CustomerLastName,
            CustomerEmail = booking.CustomerEmail,
            RoomId = booking.RoomId,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Currency = booking.Currency,
            Status = booking.Status,
            CanCheckIn = canCheckIn,
            CanCheckOut = canCheckOut
        };

        return Result<KioskBookingDto>.Success(dto);
    }
}
