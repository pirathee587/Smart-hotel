using SmartHotel.Booking.Domain.Common;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Domain.Entities;

public class ComplaintTimelineEntry : BaseEntity
{
    public Guid ComplaintId { get; set; }
    public ComplaintStatus FromStatus { get; set; }
    public ComplaintStatus ToStatus { get; set; }
    public string Note { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public Complaint? Complaint { get; set; }
}
