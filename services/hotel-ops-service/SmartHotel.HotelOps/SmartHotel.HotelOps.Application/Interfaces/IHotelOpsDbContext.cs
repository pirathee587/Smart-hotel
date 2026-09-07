using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Domain.Entities;

namespace SmartHotel.HotelOps.Application.Interfaces;

public interface IHotelOpsDbContext
{
    DbSet<Hotel> Hotels { get; }
    DbSet<Department> Departments { get; }
    DbSet<RoomType> RoomTypes { get; }
    DbSet<RoomTypeImage> RoomTypeImages { get; }
    DbSet<Room> Rooms { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
