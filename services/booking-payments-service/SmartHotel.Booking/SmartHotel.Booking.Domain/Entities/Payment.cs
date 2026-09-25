using SmartHotel.Booking.Domain.Common;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public PaymentProvider Provider { get; set; } = PaymentProvider.PayHere;
    public string PayHereOrderId { get; set; } = string.Empty;
    public string? PayHerePaymentId { get; set; }
    public string? ProviderPaymentReference { get; set; }
    public string? ProviderCheckoutReference { get; set; }
    public decimal RefundedAmount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Created;
    public DateTime? CapturedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundReason { get; set; }
    public DateTime? FundsHeldAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public DateTime? SettledAtUtc { get; set; }

    public Booking? Booking { get; set; }
    public ICollection<EscrowLifecycleRecord> EscrowLifecycle { get; set; } = new List<EscrowLifecycleRecord>();

    public void MarkCompleted(string paymentId)
    {
        Status = PaymentStatus.Completed;
        PayHerePaymentId = paymentId;
        CapturedAt = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = PaymentStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyRefund(decimal refundAmount, string reason)
    {
        if (Status != PaymentStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot refund payment with status {Status}. Only Completed payments can be refunded.");
        }
        Status = PaymentStatus.Refunded;
        RefundAmount = refundAmount;
        RefundReason = reason;
        RefundedAt = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
