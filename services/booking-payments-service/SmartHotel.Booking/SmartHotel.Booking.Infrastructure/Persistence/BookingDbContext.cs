using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;

namespace SmartHotel.Booking.Infrastructure.Persistence;

public class BookingDbContext : DbContext, IBookingDbContext
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _inMemoryLocks = new();

    public DbSet<Domain.Entities.Booking> Bookings => Set<Domain.Entities.Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<ComplaintTimelineEntry> ComplaintTimelineEntries => Set<ComplaintTimelineEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Booking
        modelBuilder.Entity<Domain.Entities.Booking>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.BookingReference).IsRequired().HasMaxLength(50);
            entity.Property(b => b.CustomerLastName).IsRequired().HasMaxLength(100);
            entity.Property(b => b.CustomerEmail).IsRequired().HasMaxLength(150);
            entity.Property(b => b.TotalAmount).HasPrecision(18, 2);
            entity.Property(b => b.PaymentReference).HasMaxLength(100);
            entity.Property(b => b.PayHereOrderId).HasMaxLength(100);

            entity.HasIndex(b => b.BookingReference).IsUnique();
            entity.HasIndex(b => new { b.RoomId, b.CheckInDate, b.CheckOutDate });
            entity.HasIndex(b => b.CustomerId);
            entity.HasIndex(b => b.Status);

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
            entity.Property(p => p.Currency).IsRequired().HasMaxLength(10);
            entity.Property(p => p.PayHereOrderId).IsRequired().HasMaxLength(100);
            entity.Property(p => p.PayHerePaymentId).HasMaxLength(100);
            entity.Property(p => p.RefundReason).HasMaxLength(500);

            entity.HasIndex(p => p.PayHereOrderId);
            entity.HasIndex(p => p.BookingId);
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

    public async Task<IDisposable?> AcquireRoomLockAsync(Guid roomId, CancellationToken ct = default)
    {
        var isPostgres = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        if (isPostgres)
        {
            // PostgreSQL transaction-level advisory lock prevents concurrent double-bookings
            var tx = Database.CurrentTransaction ?? await Database.BeginTransactionAsync(ct);
            await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({roomId}));", ct);

            return new TransactionScopeReleaser(tx);
        }

        // In-memory fallback (for unit / integration tests)
        var semaphore = _inMemoryLocks.GetOrAdd(roomId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        return new SemaphoreReleaser(semaphore);
    }

    private sealed class TransactionScopeReleaser : IDisposable
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _tx;
        private bool _disposed;

        public TransactionScopeReleaser(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx)
        {
            _tx = tx;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // pg_advisory_xact_lock releases automatically upon commit/rollback
                _tx.Dispose();
                _disposed = true;
            }
        }
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
