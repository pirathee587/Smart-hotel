namespace SmartHotel.HotelOps.Application.Interfaces;

/// <summary>
/// Contract for checking room booking availability by querying the booking-payments-service.
/// Implemented in the Infrastructure layer via HTTP.
/// </summary>
public interface IBookingAvailabilityClient
{
    /// <summary>
    /// Returns the count of rooms (for a given room type) that have no booking conflict
    /// in the requested date range, and the Id of the first available room.
    /// </summary>
    Task<RoomAvailabilityResult> GetAvailabilityAsync(
        Guid roomTypeId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken ct = default);
}

public record RoomAvailabilityResult(
    Guid RoomTypeId,
    int TotalRooms,
    int AvailableCount,
    Guid? FirstAvailableRoomId);
