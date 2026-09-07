using SmartHotel.Notifications.Domain;

namespace SmartHotel.Notifications.Application;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    NotificationType Type,
    string? PayloadJson,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt
);

public record SendNotificationRequest(
    Guid UserId,
    string Title,
    string Message,
    NotificationType Type,
    string? PayloadJson
);

public record ChatParticipantDto(
    Guid Id,
    Guid ChatRoomId,
    Guid UserId,
    string UserName,
    string UserRole,
    DateTime JoinedAt,
    DateTime? LastReadAt
);

public record ChatMessageDto(
    Guid Id,
    Guid ChatRoomId,
    Guid SenderId,
    string SenderName,
    string SenderRole,
    string Content,
    DateTime SentAt,
    bool IsRead
);

public record ChatRoomDto(
    Guid Id,
    string Title,
    ChatRoomType Type,
    Guid? TaskId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ChatParticipantDto> Participants,
    ChatMessageDto? LastMessage
);

public record CreateDirectChatRequest(
    Guid TargetUserId,
    string TargetUserName,
    string TargetUserRole
);

public record CreateTaskChatRequest(
    Guid TaskId,
    string TaskTitle,
    List<TaskParticipantRequest> Participants
);

public record TaskParticipantRequest(
    Guid UserId,
    string UserName,
    string UserRole
);

public record SendChatMessageRequest(
    string Content
);
