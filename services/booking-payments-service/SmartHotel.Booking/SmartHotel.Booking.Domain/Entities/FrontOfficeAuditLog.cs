using SmartHotel.Booking.Domain.Common;

namespace SmartHotel.Booking.Domain.Entities;

public class FrontOfficeAuditLog : BaseEntity
{
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // CheckIn, CheckOut, RoomAssignment, StaffCancellationOverride
    public Guid? ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string Source { get; set; } = "FrontOffice"; // FrontOffice, Kiosk, System
    public string? Reason { get; set; }
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
