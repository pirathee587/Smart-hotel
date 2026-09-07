using SmartHotel.Booking.Domain.Common;

namespace SmartHotel.Booking.Domain.Entities;

public class Review : BaseEntity
{
    public Guid BookingId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid RoomTypeId { get; set; }
    public int Rating { get; set; } // 1 to 5
    public string Comment { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;

    public Booking? Booking { get; set; }

    public void Hide()
    {
        IsPublished = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Unhide()
    {
        IsPublished = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
