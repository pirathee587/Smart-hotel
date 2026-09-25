using SmartHotel.HotelOps.Domain.Common;

namespace SmartHotel.HotelOps.Domain.Entities;

public enum MaintenanceRestrictionStatus { Active, Cleared }
public class MaintenanceRestriction : BaseEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid RoomId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid ReportedBy { get; set; }
    public Guid? ClearedBy { get; set; }
    public string Issue { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public bool SafetyHazard { get; set; }
    public MaintenanceRestrictionStatus Status { get; set; } = MaintenanceRestrictionStatus.Active;
    public DateTime ReportedAtUtc { get; set; }
    public DateTime? ClearedAtUtc { get; set; }
}
