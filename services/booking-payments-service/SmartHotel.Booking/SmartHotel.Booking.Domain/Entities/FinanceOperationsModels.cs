using SmartHotel.Booking.Domain.Common;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Domain.Entities;

public class FinanceInvoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceType Type { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public Guid? BookingId { get; set; }
    public Guid? PaymentId { get; set; }
    public string ChargeReference { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string PartyEmail { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ValidatedByUserId { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
}

public class FinanceCreditNote : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public FinanceInvoice? Invoice { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Reason { get; set; } = string.Empty;
    public CreditNoteStatus Status { get; set; } = CreditNoteStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public Guid? AuthorizedByUserId { get; set; }
}

/// <summary>Finance approval snapshot of a payroll calculated by the existing Field Ops payroll service.</summary>
public class FinancePayrollRecord : BaseEntity
{
    public Guid SourcePayrollId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetSalary { get; set; }
    public string Currency { get; set; } = "LKR";
    public PayrollApprovalStatus Status { get; set; } = PayrollApprovalStatus.PendingVerification;
    public Guid SubmittedByUserId { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public Guid? ManagerApprovedByUserId { get; set; }
    public Guid? OwnerApprovedByUserId { get; set; }
    public DateTime? DisbursedAtUtc { get; set; }
    public string? DisbursementReference { get; set; }
    public Guid? SalaryStructureId { get; set; }
    public Guid DepartmentId { get; set; }
    public string EmployeeRole { get; set; } = "Employee";
    public decimal OvertimeHours { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal GrossSalary { get; set; }
    public string CalculationSnapshotJson { get; set; } = "{}";
    public string AttendanceSnapshotJson { get; set; } = "{}";
}

public class SalaryStructure : BaseEntity
{
    public Guid DepartmentId { get; set; }
    public string EmployeeRole { get; set; } = "Employee";
    public decimal MonthlyBasicSalary { get; set; }
    public decimal OvertimeHourlyRate { get; set; }
    public string Currency { get; set; } = "LKR";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public int Revision { get; set; } = 1;
    public bool IsApproved { get; set; } = true;
    public Guid ApprovedByOwnerId { get; set; }
}

public class EmployeeSalaryOverride : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public decimal MonthlyBasicSalary { get; set; }
    public decimal? OvertimeHourlyRate { get; set; }
    public string Currency { get; set; } = "LKR";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ApprovedByOwnerId { get; set; }
}

public class SalaryAllowanceConfiguration : BaseEntity
{
    public Guid? DepartmentId { get; set; }
    public string? EmployeeRole { get; set; }
    public Guid? EmployeeId { get; set; }
    public AllowanceKind Kind { get; set; }
    public AllowanceCalculationType CalculationType { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = "LKR";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public Guid ApprovedByOwnerId { get; set; }
}

public class MockPayrollPayment : BaseEntity
{
    public Guid PayrollId { get; set; }
    public FinancePayrollRecord? Payroll { get; set; }
    public string InstructionReference { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public MockPaymentScenario Scenario { get; set; }
    public MockPayrollPaymentStatus Status { get; set; } = MockPayrollPaymentStatus.Processing;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public int AttemptCount { get; set; }
    public string? MockProviderReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public Guid ProcessedByUserId { get; set; }
}

public class SettlementReconciliation : BaseEntity
{
    public Guid SettlementId { get; set; }
    public SettlementRecord? Settlement { get; set; }
    public decimal ProviderReportedAmount { get; set; }
    public decimal ProviderFee { get; set; }
    public decimal BankReceivedAmount { get; set; }
    public decimal DifferenceAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Draft;
    public string? Notes { get; set; }
    public Guid PreparedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
