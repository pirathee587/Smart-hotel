using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Domain.Entities;

namespace SmartHotel.Booking.Application.Interfaces;

public interface IBookingDbContext
{
    DbSet<Domain.Entities.Booking> Bookings { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Complaint> Complaints { get; }
    DbSet<ComplaintTimelineEntry> ComplaintTimelineEntries { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a PostgreSQL transaction-level advisory lock on the given RoomId to prevent concurrent double-booking.
    /// Returns null or an IDisposable scope in environments (like InMemory) where advisory locks are mocked.
    /// </summary>
    Task<IDisposable?> AcquireRoomLockAsync(Guid roomId, CancellationToken ct = default);
}
