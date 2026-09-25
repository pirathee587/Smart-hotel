namespace SmartHotel.Booking.Domain.Enums;

public enum FinanceRequestStatus { Pending, Approved, Rejected, Cancelled }
public enum FinanceApprovalStage { Manager, Owner, Complete }
public enum FinanceExecutionStatus { NotRequired, NotStarted, Processing, Executed, Failed }
public enum FinanceRequestType { Expense, Refund }
public enum EscrowEventType { CheckoutCreated, FundsHeld, ReleaseRequested, Released, SettlementConfirmed, RefundRequested, Refunded, Failed }
public enum SettlementStatus { Pending, Confirmed, Failed }
public enum EscrowApprovalAction { Submitted, Approved, Rejected, Executed }
public enum InvoiceType { Customer, Supplier }
public enum InvoiceStatus { Draft, Validated, Issued, PartiallyPaid, Settled, Cancelled }
public enum CreditNoteStatus { Draft, Authorized, Issued, Cancelled }
public enum PayrollApprovalStatus { PendingVerification, ManagerReview, OwnerApproval, Approved, Rejected, DisbursementPending, Disbursed, Failed }
public enum ReconciliationStatus { Draft, DifferencesFound, ManagerReview, Completed }
public enum ExpenseVerificationStatus { Pending, Verified, Rejected }
public enum AllowanceKind { Transport, Meal, Attendance, NightShift, Overtime, Special }
public enum AllowanceCalculationType { FixedAmount, PercentageOfBasic }
public enum MockPayrollPaymentStatus { Processing, MockPaid, PaymentFailed, TimedOut, Delayed }
public enum MockPaymentScenario { Success, Failure, Timeout, DelayedSuccess }

public static class FinancePermissions
{
    public const string ViewReports = "finance.reports.view";
    public const string ApproveExpenses = "finance.expenses.approve";
    public const string ApproveRefunds = "finance.refunds.approve";
    public const string SubmitPayroll = "finance.payroll.submit";
    public const string ExecutePayments = "finance.payments.execute";
    public const string ApproveReleases = "finance.releases.approve";
    public const string ManageConfiguration = "finance.configuration.manage";
    public const string ViewAudit = "finance.audit.view";
    public const string ManageInvoices = "finance.invoices.manage";
    public const string VerifyExpenses = "finance.expenses.verify";
    public const string ReconcileSettlements = "finance.reconciliation.manage";
    public const string ViewPayroll = "finance.payroll.view";
    public const string ApprovePayroll = "finance.payroll.approve";
}
