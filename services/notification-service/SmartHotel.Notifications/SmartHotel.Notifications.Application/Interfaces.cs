using SmartHotel.Notifications.Domain;

namespace SmartHotel.Notifications.Application;

public interface INotificationRepository
{
    Task<Notification> AddAsync(Notification notification, CancellationToken ct = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, int limit = 50, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}

public interface IChatRepository
{
    Task<ChatRoom> CreateRoomAsync(ChatRoom room, CancellationToken ct = default);
    Task<ChatRoom?> GetRoomByIdAsync(Guid roomId, CancellationToken ct = default);
    Task<ChatRoom?> GetRoomByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    Task<ChatRoom?> FindDirectRoomAsync(Guid user1Id, Guid user2Id, CancellationToken ct = default);
    Task<IReadOnlyList<ChatRoom>> GetRoomsByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid roomId, int limit = 50, CancellationToken ct = default);
    Task MarkRoomMessagesAsReadAsync(Guid roomId, Guid userId, CancellationToken ct = default);
}

public interface INotificationHubClient
{
    Task ReceiveNotification(NotificationDto notification);
    Task ReceiveTaskAssigned(object payload);
    Task ReceiveRoomStatusUpdated(object payload);
    Task ReceiveDeliveryAlert(object payload);
    Task ReceiveChatMessage(ChatMessageDto message);
    Task UserJoinedRoom(Guid roomId, Guid userId, string userName);
    Task UserLeftRoom(Guid roomId, Guid userId, string userName);
}

public interface INotificationService
{
    Task<NotificationDto> SendNotificationAsync(Guid userId, string title, string message, NotificationType type, string? payloadJson = null, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}

public interface IChatService
{
    Task<ChatRoomDto> GetOrCreateDirectRoomAsync(Guid currentUserId, string currentUserName, string currentUserRole, Guid targetUserId, string targetUserName, string targetUserRole, CancellationToken ct = default);
    Task<ChatRoomDto> CreateTaskGroupRoomAsync(Guid taskId, string taskTitle, List<(Guid UserId, string UserName, string Role)> participants, CancellationToken ct = default);
    Task<IReadOnlyList<ChatRoomDto>> GetUserRoomsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessageDto>> GetRoomMessagesAsync(Guid roomId, Guid userId, int limit = 50, CancellationToken ct = default);
    Task<ChatMessageDto> SendMessageAsync(Guid roomId, Guid senderId, string senderName, string senderRole, string content, CancellationToken ct = default);
    Task MarkRoomAsReadAsync(Guid roomId, Guid userId, CancellationToken ct = default);
}

public interface ISignalRNotificationDispatcher
{
    Task SendNotificationToUserAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);
    Task SendNotificationToGroupAsync(string groupName, NotificationDto notification, CancellationToken ct = default);
    Task BroadcastTaskAssignedAsync(Guid employeeId, object payload, CancellationToken ct = default);
    Task BroadcastRoomStatusUpdatedAsync(string[] roles, object payload, CancellationToken ct = default);
    Task BroadcastDeliveryAlertAsync(string[] roles, object payload, CancellationToken ct = default);
    Task BroadcastChatMessageAsync(Guid roomId, ChatMessageDto message, CancellationToken ct = default);
}

