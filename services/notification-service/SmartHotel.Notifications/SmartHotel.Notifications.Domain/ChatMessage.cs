namespace SmartHotel.Notifications.Domain;

public class ChatMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ChatRoomId { get; private set; }
    public Guid SenderId { get; private set; }
    public string SenderName { get; private set; } = string.Empty;
    public string SenderRole { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; } = DateTime.UtcNow;
    public bool IsRead { get; private set; }

    public ChatRoom? ChatRoom { get; private set; }

    private ChatMessage() { } // EF Core

    public ChatMessage(
        Guid chatRoomId,
        Guid senderId,
        string senderName,
        string senderRole,
        string content)
    {
        Id = Guid.NewGuid();
        ChatRoomId = chatRoomId;
        SenderId = senderId;
        SenderName = senderName ?? string.Empty;
        SenderRole = senderRole ?? string.Empty;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        SentAt = DateTime.UtcNow;
        IsRead = false;
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
