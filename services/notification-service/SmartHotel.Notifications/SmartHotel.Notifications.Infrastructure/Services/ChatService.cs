using Microsoft.Extensions.Logging;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Domain;

namespace SmartHotel.Notifications.Infrastructure.Services;

public class ChatService : IChatService
{
    private readonly IChatRepository _chatRepository;
    private readonly INotificationService _notificationService;
    private readonly ISignalRNotificationDispatcher _dispatcher;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IChatRepository chatRepository,
        INotificationService notificationService,
        ISignalRNotificationDispatcher dispatcher,
        ILogger<ChatService> logger)
    {
        _chatRepository = chatRepository;
        _notificationService = notificationService;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<ChatRoomDto> GetOrCreateDirectRoomAsync(
        Guid currentUserId,
        string currentUserName,
        string currentUserRole,
        Guid targetUserId,
        string targetUserName,
        string targetUserRole,
        CancellationToken ct = default)
    {
        var existing = await _chatRepository.FindDirectRoomAsync(currentUserId, targetUserId, ct);
        if (existing != null)
        {
            return MapToRoomDto(existing);
        }

        string title = $"{currentUserName} & {targetUserName}";
        var room = new ChatRoom(title, ChatRoomType.Direct);
        room.AddParticipant(currentUserId, currentUserName, currentUserRole);
        room.AddParticipant(targetUserId, targetUserName, targetUserRole);

        var created = await _chatRepository.CreateRoomAsync(room, ct);
        _logger.LogInformation("Created Direct chat room {RoomId} between {User1} and {User2}", created.Id, currentUserId, targetUserId);
        return MapToRoomDto(created);
    }

    public async Task<ChatRoomDto> CreateTaskGroupRoomAsync(
        Guid taskId,
        string taskTitle,
        List<(Guid UserId, string UserName, string Role)> participants,
        CancellationToken ct = default)
    {
        var existing = await _chatRepository.GetRoomByTaskIdAsync(taskId, ct);
        if (existing != null)
        {
            return MapToRoomDto(existing);
        }

        string title = $"Task: {taskTitle}";
        var room = new ChatRoom(title, ChatRoomType.Group, taskId);
        foreach (var p in participants)
        {
            room.AddParticipant(p.UserId, p.UserName, p.Role);
        }

        var created = await _chatRepository.CreateRoomAsync(room, ct);
        _logger.LogInformation("Created Task group chat room {RoomId} for task {TaskId} with {Count} participants", created.Id, taskId, participants.Count);
        return MapToRoomDto(created);
    }

    public async Task<IReadOnlyList<ChatRoomDto>> GetUserRoomsAsync(Guid userId, CancellationToken ct = default)
    {
        var rooms = await _chatRepository.GetRoomsByUserIdAsync(userId, ct);
        return rooms.Select(MapToRoomDto).ToList();
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetRoomMessagesAsync(Guid roomId, Guid userId, int limit = 50, CancellationToken ct = default)
    {
        var room = await _chatRepository.GetRoomByIdAsync(roomId, ct);
        if (room == null)
        {
            throw new KeyNotFoundException($"Chat room {roomId} not found.");
        }

        if (!room.Participants.Any(p => p.UserId == userId))
        {
            throw new UnauthorizedAccessException("User is not a participant of this chat room.");
        }

        var messages = await _chatRepository.GetMessagesAsync(roomId, limit, ct);
        return messages.Select(MapToMessageDto).ToList();
    }

    public async Task<ChatMessageDto> SendMessageAsync(
        Guid roomId,
        Guid senderId,
        string senderName,
        string senderRole,
        string content,
        CancellationToken ct = default)
    {
        var room = await _chatRepository.GetRoomByIdAsync(roomId, ct);
        if (room == null)
        {
            throw new KeyNotFoundException($"Chat room {roomId} not found.");
        }

        if (!room.Participants.Any(p => p.UserId == senderId))
        {
            throw new UnauthorizedAccessException("User is not a participant of this chat room.");
        }

        var message = new ChatMessage(roomId, senderId, senderName, senderRole, content);
        var savedMessage = await _chatRepository.AddMessageAsync(message, ct);
        var messageDto = MapToMessageDto(savedMessage);

        // 1. Broadcast to SignalR room group
        try
        {
            await _dispatcher.BroadcastChatMessageAsync(roomId, messageDto, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast chat message {MessageId} to SignalR room {RoomId}", savedMessage.Id, roomId);
        }

        // 2. Offline Fallback: Notify participants other than the sender
        var otherParticipants = room.Participants.Where(p => p.UserId != senderId).ToList();
        foreach (var participant in otherParticipants)
        {
            try
            {
                await _notificationService.SendNotificationAsync(
                    participant.UserId,
                    $"New message from {senderName}",
                    content.Length > 100 ? content[..97] + "..." : content,
                    NotificationType.ChatMessage,
                    payloadJson: System.Text.Json.JsonSerializer.Serialize(new { roomId, messageId = savedMessage.Id, senderId }),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create message notification for participant {UserId}", participant.UserId);
            }
        }

        return messageDto;
    }

    public async Task MarkRoomAsReadAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        await _chatRepository.MarkRoomMessagesAsReadAsync(roomId, userId, ct);
    }

    private static ChatRoomDto MapToRoomDto(ChatRoom room)
    {
        var participants = room.Participants.Select(p => new ChatParticipantDto(
            p.Id,
            p.ChatRoomId,
            p.UserId,
            p.UserName,
            p.UserRole,
            p.JoinedAt,
            p.LastReadAt
        )).ToList();

        var lastMessage = room.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault();
        ChatMessageDto? lastMsgDto = lastMessage != null ? MapToMessageDto(lastMessage) : null;

        return new ChatRoomDto(
            room.Id,
            room.Title,
            room.Type,
            room.TaskId,
            room.CreatedAt,
            room.UpdatedAt,
            participants,
            lastMsgDto
        );
    }

    private static ChatMessageDto MapToMessageDto(ChatMessage message)
    {
        return new ChatMessageDto(
            message.Id,
            message.ChatRoomId,
            message.SenderId,
            message.SenderName,
            message.SenderRole,
            message.Content,
            message.SentAt,
            message.IsRead
        );
    }
}
