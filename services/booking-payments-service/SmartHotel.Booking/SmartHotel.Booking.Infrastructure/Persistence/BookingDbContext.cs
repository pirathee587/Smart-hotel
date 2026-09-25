using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;

namespace SmartHotel.Booking.Infrastructure.Persistence;

public class BookingDbContext : DbContext, IBookingDbContext
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _inMemoryLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _paymentLocks = new(StringComparer.Ordinal);

    public DbSet<Domain.Entities.Booking> Bookings => Set<Domain.Entities.Booking>();
    public DbSet<BookingDraft> BookingDrafts => Set<BookingDraft>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<ComplaintTimelineEntry> ComplaintTimelineEntries => Set<ComplaintTimelineEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ExpenseRequest> ExpenseRequests => Set<ExpenseRequest>();
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();
    public DbSet<FinanceApprovalSettings> FinanceApprovalSettings => Set<FinanceApprovalSettings>();
    public DbSet<FinanceAccessGrant> FinanceAccessGrants => Set<FinanceAccessGrant>();
    public DbSet<FinanceAuditLog> FinanceAuditLogs => Set<FinanceAuditLog>();
    public DbSet<EscrowReleaseRequest> EscrowReleaseRequests => Set<EscrowReleaseRequest>();
    public DbSet<EscrowLifecycleRecord> EscrowLifecycleRecords => Set<EscrowLifecycleRecord>();
    public DbSet<PaymentApprovalHistory> PaymentApprovalHistory => Set<PaymentApprovalHistory>();
    public DbSet<SettlementRecord> SettlementRecords => Set<SettlementRecord>();
    public DbSet<PaymentWebhookReceipt> PaymentWebhookReceipts => Set<PaymentWebhookReceipt>();
    public DbSet<FinanceInvoice> FinanceInvoices => Set<FinanceInvoice>();
    public DbSet<FinanceCreditNote> FinanceCreditNotes => Set<FinanceCreditNote>();
    public DbSet<FinancePayrollRecord> FinancePayrollRecords => Set<FinancePayrollRecord>();
    public DbSet<SettlementReconciliation> SettlementReconciliations => Set<SettlementReconciliation>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<EmployeeSalaryOverride> EmployeeSalaryOverrides => Set<EmployeeSalaryOverride>();
    public DbSet<SalaryAllowanceConfiguration> SalaryAllowanceConfigurations => Set<SalaryAllowanceConfiguration>();
    public DbSet<MockPayrollPayment> MockPayrollPayments => Set<MockPayrollPayment>();
    public DbSet<FrontOfficeAuditLog> FrontOfficeAuditLogs => Set<FrontOfficeAuditLog>();

    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BookingDraft
        modelBuilder.Entity<BookingDraft>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.RoomName).HasMaxLength(150);
            entity.Property(d => d.RatePlanName).HasMaxLength(150);
            entity.Property(d => d.PricePerNight).HasPrecision(18, 2);
            entity.Property(d => d.TaxesAndFees).HasPrecision(18, 2);
            entity.Property(d => d.TotalAmount).HasPrecision(18, 2);
            entity.Property(d => d.Currency).HasMaxLength(3);
            entity.Property(d => d.Status).HasMaxLength(50);
        });

        // Booking
        modelBuilder.Entity<Domain.Entities.Booking>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.BookingReference).IsRequired().HasMaxLength(50);
            entity.Property(b => b.CustomerLastName).IsRequired().HasMaxLength(100);
            entity.Property(b => b.CustomerEmail).IsRequired().HasMaxLength(150);
            entity.Property(b => b.TotalAmount).HasPrecision(18, 2);
            entity.Property(b => b.Currency).HasMaxLength(3);
            entity.Property(b => b.PaymentReference).HasMaxLength(100);
            entity.Property(b => b.PayHereOrderId).HasMaxLength(100);
            entity.Property(b => b.CheckoutInitiatedAtUtc);

            entity.HasIndex(b => b.BookingReference).IsUnique();
            entity.HasIndex(b => new { b.RoomId, b.CheckInDate, b.CheckOutDate });
            entity.HasIndex(b => b.CustomerId);
            entity.HasIndex(b => b.Status);
            entity.HasIndex(b => b.CheckoutInitiatedAtUtc)
                .HasFilter("\"Status\" = 0")
                .HasDatabaseName("IX_Bookings_PendingPayment_CheckoutInitiated");

            entity.HasMany(b => b.Payments)
                .WithOne(p => p.Booking)
                .HasForeignKey(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Review)
                .WithOne(r => r.Booking)
                .HasForeignKey<Review>(r => r.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Payment
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.RefundAmount).HasPrecision(18, 2);
            entity.Property(p => p.RefundedAmount).HasPrecision(18, 2);
            entity.Property(p => p.Currency).IsRequired().HasMaxLength(10);
            entity.Property(p => p.PayHereOrderId).IsRequired().HasMaxLength(100);
            entity.Property(p => p.PayHerePaymentId).HasMaxLength(100);
            entity.Property(p => p.RefundReason).HasMaxLength(500);
            entity.Property(p => p.ProviderPaymentReference).HasMaxLength(200);
            entity.Property(p => p.ProviderCheckoutReference).HasMaxLength(200);

            entity.HasIndex(p => p.PayHereOrderId);
            entity.HasIndex(p => new { p.Provider, p.PayHereOrderId }).IsUnique();
            entity.HasIndex(p => p.PayHerePaymentId).IsUnique().HasFilter("\"PayHerePaymentId\" IS NOT NULL");
            entity.HasIndex(p => p.BookingId);
            entity.HasIndex(p => new { p.Provider, p.ProviderPaymentReference }).IsUnique().HasFilter("\"ProviderPaymentReference\" IS NOT NULL");
            entity.HasIndex(p => p.Status);
        });

        // Review
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Comment).IsRequired().HasMaxLength(2000);

            entity.HasIndex(r => r.BookingId).IsUnique(); // Strict 1 review per completed booking rule
            entity.HasIndex(r => r.RoomTypeId);
            entity.HasIndex(r => r.CustomerId);
            entity.HasIndex(r => r.IsPublished);
        });

        // Complaint
        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Description).IsRequired().HasMaxLength(4000);
            entity.Property(c => c.ResolutionNotes).HasMaxLength(2000);

            entity.HasIndex(c => c.CustomerId);
            entity.HasIndex(c => c.BookingId);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.SlaDeadlineUtc);

            entity.HasMany(c => c.Timeline)
                .WithOne(t => t.Complaint)
                .HasForeignKey(t => t.ComplaintId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ComplaintTimelineEntry
        modelBuilder.Entity<ComplaintTimelineEntry>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Note).IsRequired().HasMaxLength(2000);
            entity.Property(t => t.ChangedBy).IsRequired().HasMaxLength(100);

            entity.HasIndex(t => t.ComplaintId);
        });

        // OutboxMessage
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).IsRequired().HasMaxLength(100);
            entity.Property(o => o.Content).IsRequired();
            entity.HasIndex(o => o.ProcessedOnUtc);
        });

        modelBuilder.Entity<FinanceRequest>().UseTpcMappingStrategy();
        ConfigureFinanceRequest(modelBuilder.Entity<ExpenseRequest>());
        modelBuilder.Entity<ExpenseRequest>(entity =>
        {
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.VendorReference).HasMaxLength(200);
            entity.Property(e => e.ReceiptUrl).HasMaxLength(1000);
            entity.Property(e => e.VerificationStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => new { e.Status, e.ApprovalStage, e.CreatedAtUtc });
            entity.HasIndex(e => e.SubmittedByDepartmentId);
        });

        ConfigureFinanceRequest(modelBuilder.Entity<RefundRequest>());
        modelBuilder.Entity<RefundRequest>(entity =>
        {
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => new { e.Status, e.ApprovalStage, e.CreatedAtUtc });
            entity.HasOne(e => e.Payment).WithMany().HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureFinanceRequest(modelBuilder.Entity<EscrowReleaseRequest>());
        modelBuilder.Entity<EscrowReleaseRequest>(entity =>
        {
            entity.Property(e => e.ProviderReleaseReference).HasMaxLength(200);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.PaymentId).IsUnique();
            entity.HasIndex(e => new { e.Status, e.ApprovalStage, e.CreatedAtUtc });
            entity.HasOne(e => e.Payment).WithMany().HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EscrowLifecycleRecord>(entity =>
        {
            entity.Property(e => e.EventType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.ProviderReference).HasMaxLength(200);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(150);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => new { e.PaymentId, e.CreatedAtUtc });
            entity.HasOne(e => e.Payment).WithMany(p => p.EscrowLifecycle).HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PaymentApprovalHistory>(entity =>
        {
            entity.Property(e => e.RequestType).HasMaxLength(30);
            entity.Property(e => e.Stage).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Action).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.HasIndex(e => new { e.RequestId, e.CreatedAtUtc });
        });
        modelBuilder.Entity<SettlementRecord>(entity =>
        {
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ProviderSettlementReference).HasMaxLength(200);
            entity.HasIndex(e => e.ProviderSettlementReference).IsUnique();
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
            entity.HasOne(e => e.Payment).WithMany().HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PaymentWebhookReceipt>(entity =>
        {
            entity.Property(e => e.ProviderEventId).HasMaxLength(200);
            entity.Property(e => e.EventType).HasMaxLength(50);
            entity.Property(e => e.PayloadHash).HasMaxLength(64);
            entity.HasIndex(e => new { e.Provider, e.ProviderEventId }).IsUnique();
        });
        modelBuilder.Entity<FinanceInvoice>(entity =>
        {
            entity.Property(e => e.InvoiceNumber).HasMaxLength(40);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ChargeReference).HasMaxLength(150);
            entity.Property(e => e.PartyName).HasMaxLength(200);
            entity.Property(e => e.PartyEmail).HasMaxLength(254);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.PaidAmount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => new { e.Type, e.ChargeReference }).IsUnique();
            entity.HasIndex(e => new { e.Status, e.DueDate });
            entity.HasIndex(e => e.BookingId);
        });
        modelBuilder.Entity<FinanceCreditNote>(entity =>
        {
            entity.Property(e => e.CreditNoteNumber).HasMaxLength(40);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.CreditNoteNumber).IsUnique();
            entity.HasIndex(e => e.InvoiceId);
            entity.HasOne(e => e.Invoice).WithMany().HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<FinancePayrollRecord>(entity =>
        {
            entity.Property(e => e.BaseSalary).HasPrecision(18, 2);
            entity.Property(e => e.Allowances).HasPrecision(18, 2);
            entity.Property(e => e.Deductions).HasPrecision(18, 2);
            entity.Property(e => e.NetSalary).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.DisbursementReference).HasMaxLength(200);
            entity.Property(e => e.EmployeeRole).HasMaxLength(50);
            entity.Property(e => e.OvertimeHours).HasPrecision(10, 2);
            entity.Property(e => e.OvertimePay).HasPrecision(18, 2);
            entity.Property(e => e.GrossSalary).HasPrecision(18, 2);
            entity.Property(e => e.CalculationSnapshotJson).IsRequired();
            entity.Property(e => e.AttendanceSnapshotJson).IsRequired();
            entity.HasIndex(e => e.SourcePayrollId).IsUnique();
            entity.HasIndex(e => new { e.EmployeeId, e.PeriodEnd });
            entity.HasIndex(e => new { e.Status, e.PeriodEnd });
        });
        modelBuilder.Entity<SettlementReconciliation>(entity =>
        {
            entity.Property(e => e.ProviderReportedAmount).HasPrecision(18, 2);
            entity.Property(e => e.ProviderFee).HasPrecision(18, 2);
            entity.Property(e => e.BankReceivedAmount).HasPrecision(18, 2);
            entity.Property(e => e.DifferenceAmount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.HasIndex(e => e.SettlementId).IsUnique();
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
            entity.HasOne(e => e.Settlement).WithMany().HasForeignKey(e => e.SettlementId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SalaryStructure>(entity =>
        {
            entity.Property(e => e.EmployeeRole).HasMaxLength(50);
            entity.Property(e => e.MonthlyBasicSalary).HasPrecision(18, 2);
            entity.Property(e => e.OvertimeHourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.HasIndex(e => new { e.DepartmentId, e.EmployeeRole, e.EffectiveFrom }).IsUnique();
            entity.HasIndex(e => new { e.DepartmentId, e.EmployeeRole, e.EffectiveTo });
        });
        modelBuilder.Entity<EmployeeSalaryOverride>(entity =>
        {
            entity.Property(e => e.MonthlyBasicSalary).HasPrecision(18, 2);
            entity.Property(e => e.OvertimeHourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.HasIndex(e => new { e.EmployeeId, e.EffectiveFrom }).IsUnique();
            entity.HasIndex(e => new { e.EmployeeId, e.EffectiveTo });
        });
        modelBuilder.Entity<SalaryAllowanceConfiguration>(entity =>
        {
            entity.Property(e => e.EmployeeRole).HasMaxLength(50);
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.CalculationType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Value).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.HasIndex(e => new { e.DepartmentId, e.EmployeeRole, e.EmployeeId, e.Kind, e.EffectiveFrom }).IsUnique();
            entity.HasIndex(e => new { e.DepartmentId, e.EmployeeRole, e.EffectiveTo });
            entity.HasIndex(e => new { e.EmployeeId, e.EffectiveTo });
        });
        modelBuilder.Entity<MockPayrollPayment>(entity =>
        {
            entity.Property(e => e.InstructionReference).HasMaxLength(80);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(100);
            entity.Property(e => e.Scenario).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.MockProviderReference).HasMaxLength(100);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.PayrollId).IsUnique();
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
            entity.HasOne(e => e.Payroll).WithMany().HasForeignKey(e => e.PayrollId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FinanceApprovalSettings>(entity =>
        {
            entity.Property(e => e.ManagerExpenseApprovalLimit).HasPrecision(18, 2);
            entity.Property(e => e.ManagerRefundApprovalLimit).HasPrecision(18, 2);
            entity.Property(e => e.ManagerReleaseApprovalLimit).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        });
        modelBuilder.Entity<FinanceAccessGrant>(entity =>
        {
            entity.Property(e => e.Permission).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.UserId, e.Permission }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.IsActive });
        });
        modelBuilder.Entity<FinanceAuditLog>(entity =>
        {
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Details).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.CreatedAtUtc });
            entity.HasIndex(e => new { e.ActorUserId, e.CreatedAtUtc });
        });

        modelBuilder.Entity<FrontOfficeAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BookingReference).HasMaxLength(50);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ActorRole).HasMaxLength(50);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.PreviousState).HasMaxLength(100);
            entity.Property(e => e.NewState).HasMaxLength(100);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.HasIndex(e => e.BookingId);
            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => e.Action);
        });
    }

    private static void ConfigureFinanceRequest<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : FinanceRequest
    {
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
        entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(100);
        entity.Property(e => e.DecisionReason).HasMaxLength(1000);
        entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        entity.Property(e => e.ApprovalStage).HasConversion<string>().HasMaxLength(20);
        entity.Property(e => e.ExecutionStatus).HasConversion<string>().HasMaxLength(20);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.Id == Guid.Empty)
            {
                entry.Entity.Id = Guid.NewGuid();
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginBookingTransactionAsync(CancellationToken ct = default)
    {
        // EF Core InMemory has no transaction support; its per-room semaphore is used instead.
        return Database.IsRelational()
            ? BeginRelationalTransactionAsync(ct)
            : Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>(null);
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginRelationalTransactionAsync(CancellationToken ct)
        => await Database.BeginTransactionAsync(ct);

    public async Task<IDisposable?> AcquireRoomLockAsync(Guid roomId, CancellationToken ct = default)
    {
        var isPostgres = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        if (isPostgres)
        {
            // PostgreSQL transaction-level advisory lock prevents concurrent double-bookings.
            // The command must already own the transaction that scopes this lock.
            if (Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException("A booking transaction must be started before acquiring a PostgreSQL room lock.");
            }

            await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({roomId.ToString()}));", ct);
            return null;
        }

        // In-memory fallback (for unit / integration tests)
        var semaphore = _inMemoryLocks.GetOrAdd(roomId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        return new SemaphoreReleaser(semaphore);
    }

    public async Task<IDisposable?> AcquirePaymentLockAsync(string orderReference, CancellationToken ct = default)
    {
        var isPostgres = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        if (isPostgres)
        {
            if (Database.CurrentTransaction is null) throw new InvalidOperationException("A payment transaction must be started before acquiring a PostgreSQL payment lock.");
            await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({$"payment:{orderReference}"}));", ct);
            return null;
        }
        var semaphore = _paymentLocks.GetOrAdd(orderReference, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct); return new SemaphoreReleaser(semaphore);
    }

    private sealed class SemaphoreReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public SemaphoreReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore.Release();
                _disposed = true;
            }
        }
    }
}
