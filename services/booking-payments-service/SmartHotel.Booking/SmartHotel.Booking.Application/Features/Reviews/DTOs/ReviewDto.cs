namespace SmartHotel.Booking.Application.Features.Reviews.DTOs;

public record ReviewDto
{
    public Guid Id { get; init; }
    public Guid BookingId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid RoomTypeId { get; init; }
    public int Rating { get; init; }
    public string Comment { get; init; } = string.Empty;
    public bool IsPublished { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
