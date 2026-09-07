using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Complaints.DTOs;

public record ComplaintTimelineEntryDto
{
    public Guid Id { get; init; }
    public ComplaintStatus FromStatus { get; init; }
    public ComplaintStatus ToStatus { get; init; }
    public string Note { get; init; } = string.Empty;
    public string ChangedBy { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
}

public record ComplaintDto
{
    public Guid Id { get; init; }
    public Guid? BookingId { get; init; }
    public Guid CustomerId { get; init; }
    public ComplaintCategory Category { get; init; }
    public ComplaintSeverity Severity { get; init; }
    public ComplaintStatus Status { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime SlaDeadlineUtc { get; init; }
    public bool IsOverdue => (Status == ComplaintStatus.Open || Status == ComplaintStatus.InProgress) && DateTime.UtcNow > SlaDeadlineUtc;
    public string? ResolutionNotes { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public List<ComplaintTimelineEntryDto> Timeline { get; init; } = new();
}
