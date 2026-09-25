namespace SmartHotel.Booking.Application.Interfaces;

public record RoomTypeInfo(
    Guid Id,
    string Name,
    decimal PricePerNight,
    decimal CleaningFee,
    decimal AmenitiesFee,
    int Capacity,
    bool IsPublished,
    bool IsActive,
    string? Currency = null);

public record RoomInfo(
    Guid Id,
    Guid RoomTypeId,
    string RoomNumber,
    int Floor,
    string Status);

public record RoomReadinessInfo(Guid RoomId, string RoomStatus, bool MaintenanceBlocked, bool HousekeepingApproved, string ReadinessStatus, bool ReadyForCheckIn, string? Blocker);

public record RoomClaimResult(bool Success, bool Claimed, Guid RoomId, Guid? BookingId, string? Blocker, string? Message);

public interface IHotelOpsClient
{
    Task<RoomTypeInfo?> GetRoomTypeAsync(Guid roomTypeId, CancellationToken ct = default);
    Task<RoomInfo?> GetRoomAsync(Guid roomId, CancellationToken ct = default);
    Task<RoomReadinessInfo?> GetRoomReadinessAsync(Guid roomId, CancellationToken ct = default);
    Task<RoomClaimResult> ClaimRoomAsync(Guid roomId, Guid bookingId, string source = "FrontOffice", CancellationToken ct = default);
    Task<bool> ReleaseRoomClaimAsync(Guid roomId, Guid bookingId, CancellationToken ct = default);
}
