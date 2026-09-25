using SmartHotel.Booking.Domain.Common;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Domain.Entities;

public class EscrowReleaseRequest : FinanceRequest
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public string? ProviderReleaseReference { get; set; }
}

public class EscrowLifecycleRecord : BaseEntity
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public EscrowEventType EventType { get; set; }
    public string ProviderReference { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public class PaymentApprovalHistory : BaseEntity
{
    public Guid RequestId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public FinanceApprovalStage Stage { get; set; }
    public EscrowApprovalAction Action { get; set; }
    public string? Reason { get; set; }
}

public class SettlementRecord : BaseEntity
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    public string ProviderSettlementReference { get; set; } = string.Empty;
    public DateTime? ConfirmedAtUtc { get; set; }
}

public class PaymentWebhookReceipt : BaseEntity
{
    public PaymentProvider Provider { get; set; }
    public string ProviderEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
}
