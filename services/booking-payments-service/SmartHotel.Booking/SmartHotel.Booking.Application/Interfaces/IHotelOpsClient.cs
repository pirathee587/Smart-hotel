namespace SmartHotel.Booking.Application.Interfaces;

public record RoomTypeInfo(
    Guid Id,
    string Name,
    decimal PricePerNight,
    decimal CleaningFee,
    decimal AmenitiesFee,
    int Capacity,
    bool IsPublished,
    bool IsActive);

public record RoomInfo(
    Guid Id,
    Guid RoomTypeId,
    string RoomNumber,
    int Floor,
    string Status);

public interface IHotelOpsClient
{
    Task<RoomTypeInfo?> GetRoomTypeAsync(Guid roomTypeId, CancellationToken ct = default);
    Task<RoomInfo?> GetRoomAsync(Guid roomId, CancellationToken ct = default);
}
