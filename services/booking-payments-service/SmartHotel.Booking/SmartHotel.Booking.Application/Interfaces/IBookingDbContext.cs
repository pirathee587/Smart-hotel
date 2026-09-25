using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartHotel.Booking.Domain.Entities;

namespace SmartHotel.Booking.Application.Interfaces;

public interface IBookingDbContext
{
    DbSet<Domain.Entities.Booking> Bookings { get; }
    DbSet<BookingDraft> BookingDrafts { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Complaint> Complaints { get; }
    DbSet<ComplaintTimelineEntry> ComplaintTimelineEntries { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<ExpenseRequest> ExpenseRequests { get; }
    DbSet<RefundRequest> RefundRequests { get; }
    DbSet<FinanceApprovalSettings> FinanceApprovalSettings { get; }
    DbSet<FinanceAccessGrant> FinanceAccessGrants { get; }
    DbSet<FinanceAuditLog> FinanceAuditLogs { get; }
    DbSet<EscrowReleaseRequest> EscrowReleaseRequests { get; }
    DbSet<EscrowLifecycleRecord> EscrowLifecycleRecords { get; }
    DbSet<PaymentApprovalHistory> PaymentApprovalHistory { get; }
    DbSet<SettlementRecord> SettlementRecords { get; }
    DbSet<PaymentWebhookReceipt> PaymentWebhookReceipts { get; }
    DbSet<FinanceInvoice> FinanceInvoices { get; }
    DbSet<FinanceCreditNote> FinanceCreditNotes { get; }
    DbSet<FinancePayrollRecord> FinancePayrollRecords { get; }
    DbSet<SettlementReconciliation> SettlementReconciliations { get; }
    DbSet<SalaryStructure> SalaryStructures { get; }
    DbSet<EmployeeSalaryOverride> EmployeeSalaryOverrides { get; }
    DbSet<SalaryAllowanceConfiguration> SalaryAllowanceConfigurations { get; }
    DbSet<MockPayrollPayment> MockPayrollPayments { get; }
    DbSet<FrontOfficeAuditLog> FrontOfficeAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);


    /// <summary>Starts the transaction owned by a booking command; InMemory returns null.</summary>
    Task<IDbContextTransaction?> BeginBookingTransactionAsync(CancellationToken ct = default);
    /// <summary>
    /// Acquires a PostgreSQL transaction-level advisory lock on the given RoomId to prevent concurrent double-booking.
    /// Returns an IDisposable scope only in environments (like InMemory) where advisory locks are mocked.
    /// PostgreSQL locks are released by the transaction owner on commit or rollback.
    /// </summary>
    Task<IDisposable?> AcquireRoomLockAsync(Guid roomId, CancellationToken ct = default);
    /// <summary>Serializes webhook processing for a provider order reference.</summary>
    Task<IDisposable?> AcquirePaymentLockAsync(string orderReference, CancellationToken ct = default);
}
