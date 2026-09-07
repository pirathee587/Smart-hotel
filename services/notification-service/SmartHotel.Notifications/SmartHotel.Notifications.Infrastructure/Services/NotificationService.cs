using Microsoft.Extensions.Logging;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Domain;

namespace SmartHotel.Notifications.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly ISignalRNotificationDispatcher _dispatcher;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        ISignalRNotificationDispatcher dispatcher,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<NotificationDto> SendNotificationAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? payloadJson = null,
        CancellationToken ct = default)
    {
        // 1. Offline Fallback: Always write to PostgreSQL database first
        var notification = new Notification(userId, title, message, type, payloadJson);
        await _repository.AddAsync(notification, ct);

        var dto = new NotificationDto(
            notification.Id,
            notification.UserId,
            notification.Title,
            notification.Message,
            notification.Type,
            notification.PayloadJson,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt
        );

        // 2. Real-Time Broadcast: Dispatch to connected SignalR user group
        try
        {
            await _dispatcher.SendNotificationToUserAsync(userId, dto, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast notification {Id} via SignalR to user {UserId}. User will view via DB on next login/connect.", notification.Id, userId);
        }

        return dto;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(
        Guid userId,
        bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, unreadOnly, limit: 50, ct);
        return list.Select(n => new NotificationDto(
            n.Id,
            n.UserId,
            n.Title,
            n.Message,
            n.Type,
            n.PayloadJson,
            n.IsRead,
            n.CreatedAt,
            n.ReadAt
        )).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await _repository.GetUnreadCountAsync(userId, ct);
    }

    public async Task MarkAsReadAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        await _repository.MarkAsReadAsync(id, userId, ct);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _repository.MarkAllAsReadAsync(userId, ct);
    }
}
