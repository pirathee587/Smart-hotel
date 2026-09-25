using SmartHotel.HotelOps.Domain.Common;

namespace SmartHotel.HotelOps.Domain.Entities;

public enum HousekeepingReadinessStatus { CleaningStarted, RecleanRequired, InspectionApproved }

public class HousekeepingReadiness : BaseEntity
{
    public Guid TaskId { get; set; }
    public Guid RoomId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid HousekeeperId { get; set; }
    public Guid? ManagerId { get; set; }
    public HousekeepingReadinessStatus Status { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? InspectedAtUtc { get; set; }
    public string? InspectionNotes { get; set; }
    public string ReadinessGate { get; set; } = "RoomStateAndMaintenanceRestrictionGate";
}

public class OperationalEventReceipt
{
    public Guid EventId { get; set; }
    public Guid TaskId { get; set; }
    public Guid RoomId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}
