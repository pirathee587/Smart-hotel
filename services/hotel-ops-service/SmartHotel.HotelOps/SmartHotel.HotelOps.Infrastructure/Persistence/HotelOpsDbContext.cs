using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;

namespace SmartHotel.HotelOps.Infrastructure.Persistence;

public class HotelOpsDbContext : DbContext, IHotelOpsDbContext
{
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<RoomTypeImage> RoomTypeImages => Set<RoomTypeImage>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<HousekeepingReadiness> HousekeepingReadinessRecords => Set<HousekeepingReadiness>();
    public DbSet<OperationalEventReceipt> OperationalEventReceipts => Set<OperationalEventReceipt>();
    public DbSet<MaintenanceRestriction> MaintenanceRestrictions => Set<MaintenanceRestriction>();

    public HotelOpsDbContext(DbContextOptions<HotelOpsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Hotel
        modelBuilder.Entity<Hotel>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Name).IsRequired().HasMaxLength(200);
            entity.Property(h => h.Address).HasMaxLength(500);
            entity.Property(h => h.Phone).HasMaxLength(50);
            entity.Property(h => h.Email).HasMaxLength(150);
            entity.Property(h => h.BaseCurrency).IsRequired().HasMaxLength(3).HasDefaultValue("LKR");
        });

        // Department
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
            entity.Property(d => d.Description).HasMaxLength(500);

            entity.HasOne(d => d.Hotel)
                .WithMany(h => h.Departments)
                .HasForeignKey(d => d.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Value comparer for JSON List<string> columns
        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        // RoomType
        modelBuilder.Entity<RoomType>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.Property(rt => rt.Name).IsRequired().HasMaxLength(100);
            entity.Property(rt => rt.Title).HasMaxLength(200);
            entity.Property(rt => rt.BedType).HasMaxLength(100);
            entity.Property(rt => rt.PricePerNight).HasPrecision(18, 2);
            entity.Property(rt => rt.CleaningFee).HasPrecision(18, 2);
            entity.Property(rt => rt.AmenitiesFee).HasPrecision(18, 2);

            entity.Property(rt => rt.Highlights)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(rt => rt.Amenities)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);

            entity.HasOne(rt => rt.Hotel)
                .WithMany(h => h.RoomTypes)
                .HasForeignKey(rt => rt.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RoomTypeImage
        modelBuilder.Entity<RoomTypeImage>(entity =>
        {
            entity.HasKey(img => img.Id);
            entity.Property(img => img.ImageUrl).IsRequired().HasMaxLength(1000);

            entity.HasOne(img => img.RoomType)
                .WithMany(rt => rt.Images)
                .HasForeignKey(img => img.RoomTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Room
        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.RoomNumber).IsRequired().HasMaxLength(50);
            entity.Property(r => r.OutOfOrderReason).HasMaxLength(500);

            entity.HasIndex(r => new { r.HotelId, r.RoomNumber }).IsUnique();
            entity.HasIndex(r => r.Status);

            entity.HasOne(r => r.Hotel)
                .WithMany(h => h.Rooms)
                .HasForeignKey(r => r.HotelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.RoomType)
                .WithMany(rt => rt.Rooms)
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // OutboxMessage
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).IsRequired().HasMaxLength(100);
            entity.Property(o => o.Content).IsRequired();
            entity.HasIndex(o => o.ProcessedOnUtc);
        });

        modelBuilder.Entity<HousekeepingReadiness>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TaskId).IsUnique();
            entity.HasIndex(x => new { x.RoomId, x.Status });
            entity.Property(x => x.InspectionNotes).HasMaxLength(1000);
            entity.Property(x => x.ReadinessGate).HasMaxLength(120);
        });

        modelBuilder.Entity<OperationalEventReceipt>(entity =>
        {
            entity.HasKey(x => x.EventId);
            entity.HasIndex(x => new { x.TaskId, x.EventType });
            entity.HasIndex(x => x.ProcessedAtUtc);
            entity.Property(x => x.EventType).IsRequired().HasMaxLength(80);
        });
        modelBuilder.Entity<MaintenanceRestriction>(entity =>
        {
            entity.HasKey(x=>x.Id); entity.HasIndex(x=>x.WorkOrderId).IsUnique();
            entity.HasIndex(x=>new{x.RoomId,x.Status}); entity.Property(x=>x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x=>x.Issue).HasMaxLength(2000).IsRequired(); entity.Property(x=>x.Severity).HasMaxLength(20);
        });
    }
}
