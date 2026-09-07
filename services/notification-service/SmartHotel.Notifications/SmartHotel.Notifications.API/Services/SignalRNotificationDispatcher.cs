using Microsoft.AspNetCore.SignalR;
using SmartHotel.Notifications.API.Hubs;
using SmartHotel.Notifications.Application;

namespace SmartHotel.Notifications.API.Services;

public class SignalRNotificationDispatcher : ISignalRNotificationDispatcher
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;
    private readonly ILogger<SignalRNotificationDispatcher> _logger;

    public SignalRNotificationDispatcher(
        IHubContext<NotificationHub, INotificationHubClient> hubContext,
        ILogger<SignalRNotificationDispatcher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationToUserAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
    {
        var groupName = $"user_{userId}";
        await _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
        _logger.LogDebug("Dispatched notification {NotificationId} to user group {GroupName}", notification.Id, groupName);
    }

    public async Task SendNotificationToGroupAsync(string groupName, NotificationDto notification, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
        _logger.LogDebug("Dispatched notification {NotificationId} to role/dept group {GroupName}", notification.Id, groupName);
    }

    public async Task BroadcastTaskAssignedAsync(Guid employeeId, object payload, CancellationToken ct = default)
    {
        var groupName = $"user_{employeeId}";
        await _hubContext.Clients.Group(groupName).ReceiveTaskAssigned(payload);
        _logger.LogInformation("Dispatched ReceiveTaskAssigned event to employee group {GroupName}", groupName);
    }

    public async Task BroadcastRoomStatusUpdatedAsync(string[] roles, object payload, CancellationToken ct = default)
    {
        await _hubContext.Clients.Groups(roles).ReceiveRoomStatusUpdated(payload);
        _logger.LogInformation("Dispatched ReceiveRoomStatusUpdated event to roles: {Roles}", string.Join(", ", roles));
    }

    public async Task BroadcastDeliveryAlertAsync(string[] roles, object payload, CancellationToken ct = default)
    {
        await _hubContext.Clients.Groups(roles).ReceiveDeliveryAlert(payload);
        _logger.LogInformation("Dispatched ReceiveDeliveryAlert event to roles: {Roles}", string.Join(", ", roles));
    }

    public async Task BroadcastChatMessageAsync(Guid roomId, ChatMessageDto message, CancellationToken ct = default)
    {
        var groupName = $"room_{roomId}";
        await _hubContext.Clients.Group(groupName).ReceiveChatMessage(message);
        _logger.LogDebug("Dispatched ReceiveChatMessage event to room group {GroupName}", groupName);
    }
}
