using System.Security.Cryptography;
using SmartHotel.Booking.Domain.Common;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Domain.Entities;

public class Booking : BaseEntity
{
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public Guid RoomTypeId { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int GuestCount { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;
    public string? PaymentReference { get; set; }
    public string? PayHereOrderId { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public Review? Review { get; set; }

    public static string GenerateBookingReference(int year = 0)
    {
        year = year == 0 ? DateTime.UtcNow.Year : year;
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // readable chars without 0/O, 1/I
        var bytes = RandomNumberGenerator.GetBytes(6);
        var suffix = new char[6];
        for (int i = 0; i < 6; i++)
        {
            suffix[i] = chars[bytes[i] % chars.Length];
        }
        return $"TH-{year}-{new string(suffix)}";
    }

    public void ConfirmPayment(string paymentReference, string payHereOrderId)
    {
        if (Status != BookingStatus.PendingPayment)
        {
            throw new InvalidOperationException($"Cannot confirm payment for booking in {Status} state.");
        }
        Status = BookingStatus.Confirmed;
        PaymentReference = paymentReference;
        PayHereOrderId = payHereOrderId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void CheckIn()
    {
        if (Status != BookingStatus.Confirmed)
        {
            throw new InvalidOperationException($"Cannot check in booking with status {Status}. Only Confirmed bookings can check in.");
        }
        Status = BookingStatus.CheckedIn;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void CheckOut()
    {
        if (Status != BookingStatus.CheckedIn)
        {
            throw new InvalidOperationException($"Cannot check out booking with status {Status}. Only CheckedIn bookings can check out.");
        }
        Status = BookingStatus.CheckedOut;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled || Status == BookingStatus.CheckedOut)
        {
            throw new InvalidOperationException($"Cannot cancel booking with status {Status}.");
        }
        Status = BookingStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
