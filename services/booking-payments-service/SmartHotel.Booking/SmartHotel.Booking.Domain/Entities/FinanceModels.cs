using SmartHotel.Booking.Domain.Common;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Domain.Entities;

public class FinanceApprovalSettings : BaseEntity
{
    public decimal ManagerExpenseApprovalLimit { get; set; }
    public decimal ManagerRefundApprovalLimit { get; set; }
    public decimal ManagerReleaseApprovalLimit { get; set; }
    public string Currency { get; set; } = "LKR";
    public Guid UpdatedByUserId { get; set; }
}

public abstract class FinanceRequest : BaseEntity
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Description { get; set; } = string.Empty;
    public FinanceRequestStatus Status { get; set; } = FinanceRequestStatus.Pending;
    public FinanceApprovalStage ApprovalStage { get; set; } = FinanceApprovalStage.Manager;
    public FinanceExecutionStatus ExecutionStatus { get; set; } = FinanceExecutionStatus.NotStarted;
    public Guid SubmittedByUserId { get; set; }
    public Guid SubmittedByDepartmentId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
}

public class ExpenseRequest : FinanceRequest
{
    public string Category { get; set; } = string.Empty;
    public string? VendorReference { get; set; }
    public string? ReceiptUrl { get; set; }
    public ExpenseVerificationStatus VerificationStatus { get; set; } = ExpenseVerificationStatus.Pending;
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
}

public class RefundRequest : FinanceRequest
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }
}

public class FinanceAccessGrant : BaseEntity
{
    public Guid UserId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public Guid GrantedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FinanceAuditLog : BaseEntity
{
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
}
