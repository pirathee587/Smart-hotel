namespace SmartHotel.Notifications.Domain;

public class ChatParticipant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ChatRoomId { get; private set; }
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = string.Empty;
    public DateTime JoinedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? LastReadAt { get; private set; }

    public ChatRoom? ChatRoom { get; private set; }

    private ChatParticipant() { } // EF Core

    public ChatParticipant(Guid chatRoomId, Guid userId, string userName, string userRole)
    {
        Id = Guid.NewGuid();
        ChatRoomId = chatRoomId;
        UserId = userId;
        UserName = userName ?? string.Empty;
        UserRole = userRole ?? string.Empty;
        JoinedAt = DateTime.UtcNow;
    }

    public void UpdateLastRead()
    {
        LastReadAt = DateTime.UtcNow;
    }
}
