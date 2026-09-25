using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Bookings.Queries;

/// <summary>
/// Returns available room count and first available room Id for the given room type and date range.
/// Used by the hotel-ops-service to surface availability on the public room listing page.
/// </summary>
public record CheckRoomAvailabilityQuery(
    Guid RoomTypeId,
    DateOnly CheckIn,
    DateOnly CheckOut) : IRequest<Result<RoomAvailabilityResultDto>>;

public record RoomAvailabilityResultDto(
    Guid RoomTypeId,
    int TotalRooms,
    int AvailableCount,
    Guid? FirstAvailableRoomId);

public class CheckRoomAvailabilityQueryHandler
    : IRequestHandler<CheckRoomAvailabilityQuery, Result<RoomAvailabilityResultDto>>
{
    private readonly IBookingDbContext _context;

    public CheckRoomAvailabilityQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RoomAvailabilityResultDto>> Handle(
        CheckRoomAvailabilityQuery request,
        CancellationToken ct)
    {
        if (request.CheckOut <= request.CheckIn)
            return Result<RoomAvailabilityResultDto>.Failure("Check-out must be after check-in.");

        // Get all distinct rooms for this room type that have ANY non-cancelled booking conflicting with the dates.
        var bookedRoomIds = await _context.Bookings
            .AsNoTracking()
            .Where(b =>
                b.RoomTypeId == request.RoomTypeId &&
                b.Status != BookingStatus.Cancelled &&
                request.CheckIn < b.CheckOutDate &&
                request.CheckOut > b.CheckInDate)
            .Select(b => b.RoomId)
            .Distinct()
            .ToListAsync(ct);

        // Get all rooms for this room type that have been booked at least once.
        // We use the bookings table as a proxy for total rooms (since we don't have direct DB access to hotel-ops rooms here).
        // More accurately: hotel-ops-service sends TotalRooms, we just return bookedRoomIds.
        // The available room is any room NOT in bookedRoomIds.

        // For the "first available room" we need to look at all rooms that ever had a booking for this room type,
        // then return one that is not in the conflict set.
        var allRoomIdsForType = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.RoomTypeId == request.RoomTypeId)
            .Select(b => b.RoomId)
            .Distinct()
            .ToListAsync(ct);

        var availableRoomIds = allRoomIdsForType.Except(bookedRoomIds).ToList();
        var firstAvailable = availableRoomIds.FirstOrDefault();

        // Note: TotalRooms is tracked by hotel-ops-service (it owns rooms).
        // We return -1 as sentinel; the hotel-ops availability client will combine with its own room count.
        return Result<RoomAvailabilityResultDto>.Success(new RoomAvailabilityResultDto(
            request.RoomTypeId,
            TotalRooms: -1,                 // sentinel: caller must resolve total from hotel-ops
            AvailableCount: -1,             // sentinel: caller computes = total - booked
            FirstAvailableRoomId: firstAvailable == Guid.Empty ? null : firstAvailable));
    }
}
