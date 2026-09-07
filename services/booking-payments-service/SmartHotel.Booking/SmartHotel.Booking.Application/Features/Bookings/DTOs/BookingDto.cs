using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Bookings.DTOs;

public record BookingDto
{
    public Guid Id { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerLastName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public Guid RoomId { get; init; }
    public string RoomNumber { get; init; } = string.Empty;
    public Guid RoomTypeId { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public int GuestCount { get; init; }
    public decimal TotalAmount { get; init; }
    public BookingStatus Status { get; init; }
    public string? PaymentReference { get; init; }
    public string? PayHereOrderId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
