using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Rooms.Queries;

/// <summary>
/// Returns availability for all published+active room types for the given date range.
/// Optionally filter by a specific roomTypeId.
/// </summary>
public record GetAvailabilityQuery(
    DateOnly CheckIn,
    DateOnly CheckOut,
    Guid? RoomTypeId = null) : IRequest<Result<List<RoomAvailabilityDto>>>;

public record RoomAvailabilityDto(
    Guid RoomTypeId,
    string RoomTypeName,
    int TotalRooms,
    int AvailableCount,
    Guid? FirstAvailableRoomId);

public class GetAvailabilityQueryHandler
    : IRequestHandler<GetAvailabilityQuery, Result<List<RoomAvailabilityDto>>>
{
    private readonly IHotelOpsDbContext _context;
    private readonly IBookingAvailabilityClient _availabilityClient;

    public GetAvailabilityQueryHandler(
        IHotelOpsDbContext context,
        IBookingAvailabilityClient availabilityClient)
    {
        _context = context;
        _availabilityClient = availabilityClient;
    }

    public async Task<Result<List<RoomAvailabilityDto>>> Handle(
        GetAvailabilityQuery request,
        CancellationToken ct)
    {
        if (request.CheckOut <= request.CheckIn)
            return Result<List<RoomAvailabilityDto>>.Failure("Check-out must be after check-in.");

        if (request.CheckIn < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<List<RoomAvailabilityDto>>.Failure("Check-in date cannot be in the past.");

        // Fetch published room types with their room counts from hotel-ops DB
        var query = _context.RoomTypes
            .Include(rt => rt.Rooms)
            .Where(rt => rt.IsPublished && rt.IsActive)
            .AsNoTracking();

        if (request.RoomTypeId.HasValue)
            query = query.Where(rt => rt.Id == request.RoomTypeId.Value);

        var roomTypes = await query
            .Select(rt => new
            {
                rt.Id,
                rt.Name,
                TotalRooms = rt.Rooms.Count(r => r.Status == SmartHotel.HotelOps.Domain.Enums.RoomStatus.Available),
                AllRoomIds = rt.Rooms
                    .Where(r => r.Status == SmartHotel.HotelOps.Domain.Enums.RoomStatus.Available)
                    .Select(r => r.Id)
                    .ToList()
            })
            .ToListAsync(ct);

        // Query availability from booking service in parallel
        var tasks = roomTypes.Select(async rt =>
        {
            if (rt.TotalRooms == 0)
            {
                return new RoomAvailabilityDto(rt.Id, rt.Name, 0, 0, null);
            }

            var avail = await _availabilityClient.GetAvailabilityAsync(
                rt.Id, request.CheckIn, request.CheckOut, ct);

            // FirstAvailableRoomId from booking service may be null if no bookings exist yet
            // In that case, any room is available — pick first from hotel-ops DB
            Guid? firstAvailableId = avail.FirstAvailableRoomId
                ?? (rt.AllRoomIds.Count > 0 ? rt.AllRoomIds[0] : (Guid?)null);

            // We trust the FirstAvailableRoomId from the booking service;
            // for count, if the booking service is unavailable (sentinel -1), assume all rooms available
            var bookedCount = avail.AvailableCount == -1 ? 0 : (rt.TotalRooms - avail.AvailableCount);
            var availableCount = rt.TotalRooms - Math.Max(0, bookedCount);
            if (availableCount < 0) availableCount = 0;

            // If first available room from booking service is not in our DB, pick first hotel-ops room
            if (firstAvailableId.HasValue && !rt.AllRoomIds.Contains(firstAvailableId.Value))
                firstAvailableId = rt.AllRoomIds.Count > 0 ? rt.AllRoomIds[0] : (Guid?)null;


            return new RoomAvailabilityDto(
                rt.Id,
                rt.Name,
                rt.TotalRooms,
                availableCount,
                availableCount > 0 ? firstAvailableId : null);
        });

        var results = await Task.WhenAll(tasks);
        return Result<List<RoomAvailabilityDto>>.Success(results.ToList());
    }
}
