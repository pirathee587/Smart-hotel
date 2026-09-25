using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Enums;

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
            Currency = booking.Currency,
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
            Currency = booking.Currency,
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

public record GetAllBookingsQuery(
    int? Limit = null,
    BookingStatus? Status = null,
    string? Search = null,
    DateOnly? CheckInDate = null,
    DateOnly? CheckOutDate = null) : IRequest<Result<List<BookingDto>>>;

public class GetAllBookingsQueryHandler : IRequestHandler<GetAllBookingsQuery, Result<List<BookingDto>>>
{
    private readonly IBookingDbContext _context;

    public GetAllBookingsQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<BookingDto>>> Handle(GetAllBookingsQuery request, CancellationToken ct)
    {
        var query = _context.Bookings.AsNoTracking();

        if (request.Status.HasValue)
        {
            query = query.Where(b => b.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(b =>
                b.BookingReference.ToLower().Contains(search) ||
                b.CustomerLastName.ToLower().Contains(search) ||
                b.CustomerEmail.ToLower().Contains(search) ||
                b.RoomNumber.ToLower().Contains(search));
        }

        if (request.CheckInDate.HasValue)
        {
            query = query.Where(b => b.CheckInDate == request.CheckInDate.Value);
        }

        if (request.CheckOutDate.HasValue)
        {
            query = query.Where(b => b.CheckOutDate == request.CheckOutDate.Value);
        }

        query = query.OrderByDescending(b => b.CreatedAtUtc);

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
            Currency = booking.Currency,
            Status = booking.Status,
            PaymentReference = booking.PaymentReference,
            PayHereOrderId = booking.PayHereOrderId,
            CreatedAtUtc = booking.CreatedAtUtc
        }).ToList();

        return Result<List<BookingDto>>.Success(dtos);
    }
}

public record FrontOfficeSummaryDto(
    int TodayArrivals,
    int TodayDepartures,
    int PendingCheckIns,
    int PendingCheckOuts,
    int CheckedInToday,
    int TotalConfirmed,
    int TotalActiveOccupancy);

public record GetFrontOfficeSummaryQuery() : IRequest<Result<FrontOfficeSummaryDto>>;

public class GetFrontOfficeSummaryQueryHandler : IRequestHandler<GetFrontOfficeSummaryQuery, Result<FrontOfficeSummaryDto>>
{
    private readonly IBookingDbContext _context;

    public GetFrontOfficeSummaryQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<FrontOfficeSummaryDto>> Handle(GetFrontOfficeSummaryQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var bookings = await _context.Bookings.AsNoTracking().ToListAsync(ct);

        var todayArrivals = bookings.Count(b => b.CheckInDate == today && b.Status == BookingStatus.Confirmed);
        var todayDepartures = bookings.Count(b => b.CheckOutDate == today && b.Status == BookingStatus.CheckedIn);
        var pendingCheckIns = bookings.Count(b => b.Status == BookingStatus.Confirmed);
        var pendingCheckOuts = bookings.Count(b => b.Status == BookingStatus.CheckedIn);
        var checkedInToday = bookings.Count(b => b.Status == BookingStatus.CheckedIn && b.CheckInDate == today);
        var totalConfirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed);
        var totalActive = bookings.Count(b => b.Status == BookingStatus.CheckedIn);

        var dto = new FrontOfficeSummaryDto(
            todayArrivals,
            todayDepartures,
            pendingCheckIns,
            pendingCheckOuts,
            checkedInToday,
            totalConfirmed,
            totalActive);

        return Result<FrontOfficeSummaryDto>.Success(dto);
    }
}

public record FrontOfficeAuditLogDto(
    Guid Id,
    Guid BookingId,
    string BookingReference,
    string Action,
    Guid? ActorUserId,
    string ActorRole,
    string Source,
    string? Reason,
    string PreviousState,
    string NewState,
    string? Details,
    DateTime TimestampUtc);

public record GetBookingAuditLogsQuery(Guid BookingId) : IRequest<Result<List<FrontOfficeAuditLogDto>>>;

public class GetBookingAuditLogsQueryHandler : IRequestHandler<GetBookingAuditLogsQuery, Result<List<FrontOfficeAuditLogDto>>>
{
    private readonly IBookingDbContext _context;

    public GetBookingAuditLogsQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<FrontOfficeAuditLogDto>>> Handle(GetBookingAuditLogsQuery request, CancellationToken ct)
    {
        var logs = await _context.FrontOfficeAuditLogs
            .AsNoTracking()
            .Where(l => l.BookingId == request.BookingId)
            .OrderByDescending(l => l.TimestampUtc)
            .Select(l => new FrontOfficeAuditLogDto(
                l.Id,
                l.BookingId,
                l.BookingReference,
                l.Action,
                l.ActorUserId,
                l.ActorRole,
                l.Source,
                l.Reason,
                l.PreviousState,
                l.NewState,
                l.Details,
                l.TimestampUtc))
            .ToListAsync(ct);

        return Result<List<FrontOfficeAuditLogDto>>.Success(logs);
    }
}


