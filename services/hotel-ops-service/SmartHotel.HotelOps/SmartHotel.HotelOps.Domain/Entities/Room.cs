using SmartHotel.HotelOps.Domain.Common;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Domain.Exceptions;

namespace SmartHotel.HotelOps.Domain.Entities;

public class Room : BaseEntity
{
    public Guid HotelId { get; set; }
    public Guid RoomTypeId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string? OutOfOrderReason { get; set; }

    public Hotel? Hotel { get; set; }
    public RoomType? RoomType { get; set; }

    /// <summary>
    /// Enforces domain state machine transitions for room status.
    /// Valid transitions:
    /// - Available -> Occupied
    /// - Occupied -> Dirty
    /// - Dirty -> InCleaning
    /// - InCleaning -> Inspected
    /// - Inspected -> Available
    /// - Any -> OutOfOrder (requires non-empty reason)
    /// - OutOfOrder -> Dirty or Inspected
    /// </summary>
    public void UpdateStatus(RoomStatus newStatus, string? reason = null)
    {
        if (Status == newStatus)
        {
            return;
        }

        if (newStatus == RoomStatus.OutOfOrder)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidRoomStatusTransitionException(
                    Status,
                    newStatus,
                    "A non-empty reason is mandatory when transitioning a room to OutOfOrder.");
            }
            Status = newStatus;
            OutOfOrderReason = reason.Trim();
            UpdatedAtUtc = DateTime.UtcNow;
            return;
        }

        var isValid = (Status, newStatus) switch
        {
            (RoomStatus.Available, RoomStatus.Occupied) => true,
            (RoomStatus.Occupied, RoomStatus.Dirty) => true,
            (RoomStatus.Dirty, RoomStatus.InCleaning) => true,
            (RoomStatus.InCleaning, RoomStatus.Inspected) => true,
            (RoomStatus.Inspected, RoomStatus.Available) => true,
            (RoomStatus.OutOfOrder, RoomStatus.Dirty) => true,
            (RoomStatus.OutOfOrder, RoomStatus.Inspected) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidRoomStatusTransitionException(
                Status,
                newStatus,
                $"Transition from {Status} to {newStatus} is not permitted by hotel operations state machine.");
        }

        Status = newStatus;
        if (Status != RoomStatus.OutOfOrder)
        {
            OutOfOrderReason = null;
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
