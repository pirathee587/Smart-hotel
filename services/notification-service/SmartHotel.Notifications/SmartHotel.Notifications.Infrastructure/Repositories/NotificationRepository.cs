using Microsoft.EntityFrameworkCore;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Domain;
using SmartHotel.Notifications.Infrastructure.Persistence;

namespace SmartHotel.Notifications.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationsDbContext _context;

    public NotificationRepository(NotificationsDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(notification, ct);
        await _context.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(
        Guid userId,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken ct = default)
    {
        var query = _context.Notifications.Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync(ct);
    }

    public async Task MarkAsReadAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

        if (notification != null)
        {
            notification.MarkAsRead();
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        foreach (var notification in unreadNotifications)
        {
            notification.MarkAsRead();
        }

        await _context.SaveChangesAsync(ct);
    }
}
