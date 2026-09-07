using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Interfaces;

namespace SmartHotel.Booking.Application.Features.Bookings.Queries;

public record GetBookingByIdQuery(Guid BookingId) : IRequest<Result<BookingDto>>;

public class GetBookingByIdQueryHandler : IRequestHandler<GetBookingByIdQuery, Result<BookingDto>>
{
    private readonly IBookingDbContext _context;

    public GetBookingByIdQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BookingDto>> Handle(GetBookingByIdQuery request, CancellationToken ct)
    {
        var booking = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BookingId, ct);
        if (booking == null)
        {
            return Result<BookingDto>.Failure($"Booking with ID {request.BookingId} was not found.");
        }

        return Result<BookingDto>.Success(new BookingDto
        {
            Id = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            CustomerLastName = booking.CustomerLastName,
            CustomerEmail = booking.CustomerEmail,
            RoomId = booking.RoomId,
            RoomNumber = booking.RoomNumber,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            PaymentReference = booking.PaymentReference,
            PayHereOrderId = booking.PayHereOrderId,
            CreatedAtUtc = booking.CreatedAtUtc
        });
    }
}

public record GetCustomerBookingsQuery(Guid CustomerId) : IRequest<Result<List<BookingDto>>>;

public class GetCustomerBookingsQueryHandler : IRequestHandler<GetCustomerBookingsQuery, Result<List<BookingDto>>>
{
    private readonly IBookingDbContext _context;

    public GetCustomerBookingsQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<BookingDto>>> Handle(GetCustomerBookingsQuery request, CancellationToken ct)
    {
        var list = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.CustomerId == request.CustomerId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(ct);

        var dtos = list.Select(booking => new BookingDto
        {
            Id = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            CustomerLastName = booking.CustomerLastName,
            CustomerEmail = booking.CustomerEmail,
            RoomId = booking.RoomId,
            RoomNumber = booking.RoomNumber,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            PaymentReference = booking.PaymentReference,
            PayHereOrderId = booking.PayHereOrderId,
            CreatedAtUtc = booking.CreatedAtUtc
        }).ToList();

        return Result<List<BookingDto>>.Success(dtos);
    }
}

public record ActiveStayDto
{
    public Guid BookingId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public Guid RoomId { get; init; }
    public string RoomNumber { get; init; } = string.Empty;
    public Guid RoomTypeId { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public int GuestCount { get; init; }
    public Domain.Enums.BookingStatus Status { get; init; }
    public bool IsActive { get; init; }
}

public record GetActiveStayQuery(Guid CustomerId) : IRequest<Result<ActiveStayDto>>;

public class GetActiveStayQueryHandler : IRequestHandler<GetActiveStayQuery, Result<ActiveStayDto>>
{
    private readonly IBookingDbContext _context;

    public GetActiveStayQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ActiveStayDto>> Handle(GetActiveStayQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Prioritize CheckedIn bookings first, then Confirmed bookings active today
        var bookings = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.CustomerId == request.CustomerId &&
                        (b.Status == Domain.Enums.BookingStatus.CheckedIn ||
                         b.Status == Domain.Enums.BookingStatus.Confirmed))
            .OrderByDescending(b => b.Status == Domain.Enums.BookingStatus.CheckedIn)
            .ThenByDescending(b => b.CheckInDate)
            .ToListAsync(ct);

        var activeBooking = bookings.FirstOrDefault(b =>
            b.Status == Domain.Enums.BookingStatus.CheckedIn ||
            (b.CheckInDate <= today && today <= b.CheckOutDate)) ?? bookings.FirstOrDefault();

        if (activeBooking == null)
        {
            return Result<ActiveStayDto>.Failure("No active stay found for this guest.");
        }

        return Result<ActiveStayDto>.Success(new ActiveStayDto
        {
            BookingId = activeBooking.Id,
            BookingReference = activeBooking.BookingReference,
            CustomerId = activeBooking.CustomerId,
            RoomId = activeBooking.RoomId,
            RoomNumber = activeBooking.RoomNumber,
            RoomTypeId = activeBooking.RoomTypeId,
            CheckInDate = activeBooking.CheckInDate,
            CheckOutDate = activeBooking.CheckOutDate,
            GuestCount = activeBooking.GuestCount,
            Status = activeBooking.Status,
            IsActive = true
        });
    }
}

public record GetAllBookingsQuery(int? Limit = null) : IRequest<Result<List<BookingDto>>>;

public class GetAllBookingsQueryHandler : IRequestHandler<GetAllBookingsQuery, Result<List<BookingDto>>>
{
    private readonly IBookingDbContext _context;

    public GetAllBookingsQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<BookingDto>>> Handle(GetAllBookingsQuery request, CancellationToken ct)
    {
        var query = _context.Bookings
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAtUtc);

        var list = request.Limit.HasValue && request.Limit.Value > 0
            ? await query.Take(request.Limit.Value).ToListAsync(ct)
            : await query.ToListAsync(ct);

        var dtos = list.Select(booking => new BookingDto
        {
            Id = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            CustomerLastName = booking.CustomerLastName,
            CustomerEmail = booking.CustomerEmail,
            RoomId = booking.RoomId,
            RoomNumber = booking.RoomNumber,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            PaymentReference = booking.PaymentReference,
            PayHereOrderId = booking.PayHereOrderId,
            CreatedAtUtc = booking.CreatedAtUtc
        }).ToList();

        return Result<List<BookingDto>>.Success(dtos);
    }
}

