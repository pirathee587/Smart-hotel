namespace SmartHotel.Booking.Application.Features.Payments.DTOs;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Provider { get; set; } = "PayHere";
    public string PayHereOrderId { get; set; } = string.Empty;
    public string? PayHerePaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CapturedAt { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
