namespace SmartHotel.Identity.Domain.Entities;

public class GuestAccessAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string Action { get; set; } = "GuestSearch";
    public string? SearchQuery { get; set; }
    public int ResultCount { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
