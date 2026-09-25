using SmartHotel.Booking.Domain.Common;

namespace SmartHotel.Booking.Domain.Entities;

public class BookingDraft : BaseEntity
{
    public Guid CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid RoomTypeId { get; set; }
    public string RatePlanId { get; set; } = string.Empty;
    public string RatePlanName { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int GuestCount { get; set; } = 2;
    public int RoomsCount { get; set; } = 1;
    public decimal PricePerNight { get; set; }
    public int Nights { get; set; } = 2;
    public decimal TaxesAndFees { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsUpgraded { get; set; } = false;
    public Guid? OriginalRoomId { get; set; }
    public string? OriginalRoomName { get; set; }
    public bool SessionSuppressedUpsell { get; set; } = false;
    public string Status { get; set; } = "Draft";
    public string? Currency { get; set; }

    public void RecalculateTotals()
    {
        Nights = Math.Max(1, CheckOutDate.DayNumber - CheckInDate.DayNumber);
        var subtotal = PricePerNight * Nights * Math.Max(1, RoomsCount);
        TaxesAndFees = Math.Round(subtotal * 0.10m, 2); // 10% taxes & hospitality service fees
        TotalAmount = subtotal + TaxesAndFees;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyUpgrade(Guid newRoomId, string newRoomName, string newRatePlanId, string newRatePlanName, decimal newPricePerNight)
    {
        if (!IsUpgraded)
        {
            OriginalRoomId = RoomId;
            OriginalRoomName = RoomName;
        }

        RoomId = newRoomId;
        RoomName = newRoomName;
        RatePlanId = newRatePlanId;
        RatePlanName = newRatePlanName;
        PricePerNight = newPricePerNight;
        IsUpgraded = true;
        Status = "Upgraded";
        RecalculateTotals();
    }
}
