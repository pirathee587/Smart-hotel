using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Kiosk.DTOs;

public record KioskBookingDto
{
    public Guid BookingId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public string CustomerLastName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public Guid RoomId { get; init; }
    public Guid RoomTypeId { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public int GuestCount { get; init; }
    public decimal TotalAmount { get; init; }
    public BookingStatus Status { get; init; }
    public bool CanCheckIn { get; init; }
    public bool CanCheckOut { get; init; }
}
