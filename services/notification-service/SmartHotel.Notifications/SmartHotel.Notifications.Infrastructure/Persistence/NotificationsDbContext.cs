using Microsoft.EntityFrameworkCore;
using SmartHotel.Notifications.Domain;

namespace SmartHotel.Notifications.Infrastructure.Persistence;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ManagerAlert> ManagerAlerts => Set<ManagerAlert>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Notification configuration
        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("notifications");
            b.HasKey(n => n.Id);
            b.Property(n => n.Title).HasMaxLength(250).IsRequired();
            b.Property(n => n.Message).HasMaxLength(2000).IsRequired();
            b.Property(n => n.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
            b.Property(n => n.PayloadJson).HasColumnType("text");
            b.HasIndex(n => n.UserId);
            b.HasIndex(n => new { n.UserId, n.IsRead });
            b.HasIndex(n => n.CreatedAt);
        });

        modelBuilder.Entity<ManagerAlert>(b =>
        {
            b.ToTable("manager_alerts");
            b.HasKey(a => a.Id);
            b.Property(a => a.AlertType).HasMaxLength(50).IsRequired();
            b.Property(a => a.Severity).HasMaxLength(20).IsRequired();
            b.Property(a => a.BookingReference).HasMaxLength(100);
            b.Property(a => a.MessageSnippet).HasMaxLength(500).IsRequired();
            b.Property(a => a.PayloadJson).HasColumnType("text");
            b.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.HasIndex(a => a.EventId).IsUnique();
            b.HasIndex(a => new { a.Status, a.CreatedAt }).IsDescending(false, true);
            b.HasIndex(a => new { a.AlertType, a.Status });

            // Guest/room/booking/task/actor IDs are cross-service correlation IDs.
            // They intentionally have no database FKs in this polyglot architecture.
        });

        // ChatRoom configuration
        modelBuilder.Entity<ChatRoom>(b =>
        {
            b.ToTable("chat_rooms");
            b.HasKey(r => r.Id);
            b.Property(r => r.Title).HasMaxLength(250).IsRequired();
            b.Property(r => r.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
            b.HasIndex(r => r.TaskId);

            b.HasMany(r => r.Participants)
                .WithOne(p => p.ChatRoom)
                .HasForeignKey(p => p.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(r => r.Messages)
                .WithOne(m => m.ChatRoom)
                .HasForeignKey(m => m.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatParticipant configuration
        modelBuilder.Entity<ChatParticipant>(b =>
        {
            b.ToTable("chat_participants");
            b.HasKey(p => p.Id);
            b.Property(p => p.UserName).HasMaxLength(150);
            b.Property(p => p.UserRole).HasMaxLength(50);
            b.HasIndex(p => new { p.ChatRoomId, p.UserId }).IsUnique();
            b.HasIndex(p => p.UserId);
        });

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>(b =>
        {
            b.ToTable("chat_messages");
            b.HasKey(m => m.Id);
            b.Property(m => m.SenderName).HasMaxLength(150);
            b.Property(m => m.SenderRole).HasMaxLength(50);
            b.Property(m => m.Content).HasMaxLength(4000).IsRequired();
            b.HasIndex(m => m.ChatRoomId);
            b.HasIndex(m => m.SentAt);
        });
    }
}
