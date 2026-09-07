using SmartHotel.Booking.Domain.Common;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Domain.Entities;

public class Complaint : BaseEntity
{
    public Guid? BookingId { get; set; }
    public Guid CustomerId { get; set; }
    public ComplaintCategory Category { get; set; } = ComplaintCategory.Other;
    public ComplaintSeverity Severity { get; set; } = ComplaintSeverity.Medium;
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime SlaDeadlineUtc { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public ICollection<ComplaintTimelineEntry> Timeline { get; set; } = new List<ComplaintTimelineEntry>();

    public static DateTime CalculateSlaDeadline(ComplaintSeverity severity, DateTime? fromUtc = null)
    {
        var start = fromUtc ?? DateTime.UtcNow;
        return severity switch
        {
            ComplaintSeverity.Low => start.AddHours(48),
            ComplaintSeverity.Medium => start.AddHours(24),
            ComplaintSeverity.High => start.AddHours(6),
            ComplaintSeverity.Critical => start.AddHours(2),
            _ => start.AddHours(24)
        };
    }

    public ComplaintTimelineEntry AddTimelineEntry(ComplaintStatus toStatus, string note, string changedBy)
    {
        var entry = new ComplaintTimelineEntry
        {
            Id = Guid.Empty,
            ComplaintId = Id,
            FromStatus = Status,
            ToStatus = toStatus,
            Note = note,
            ChangedBy = changedBy,
            TimestampUtc = DateTime.UtcNow
        };
        Timeline.Add(entry);
        Status = toStatus;
        UpdatedAtUtc = DateTime.UtcNow;
        return entry;
    }

    public void Escalate(string reason, string changedBy = "System")
    {
        if (Status != ComplaintStatus.Resolved && Status != ComplaintStatus.Closed)
        {
            AddTimelineEntry(ComplaintStatus.Escalated, $"Escalated: {reason}", changedBy);
        }
    }

    public void Resolve(string resolutionNotes, string resolvedBy)
    {
        ResolutionNotes = resolutionNotes;
        ResolvedAtUtc = DateTime.UtcNow;
        AddTimelineEntry(ComplaintStatus.Resolved, $"Resolved: {resolutionNotes}", resolvedBy);
    }

    public void Close(string closedBy)
    {
        AddTimelineEntry(ComplaintStatus.Closed, "Complaint closed.", closedBy);
    }
}
