namespace SmartHotel.Notifications.Domain;

public class ChatRoom
{
    private readonly List<ChatParticipant> _participants = new();
    private readonly List<ChatMessage> _messages = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Title { get; private set; } = string.Empty;
    public ChatRoomType Type { get; private set; } = ChatRoomType.Direct;
    public Guid? TaskId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public IReadOnlyCollection<ChatParticipant> Participants => _participants.AsReadOnly();
    public IReadOnlyCollection<ChatMessage> Messages => _messages.AsReadOnly();

    private ChatRoom() { } // EF Core

    public ChatRoom(string title, ChatRoomType type, Guid? taskId = null)
    {
        Id = Guid.NewGuid();
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Type = type;
        TaskId = taskId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddParticipant(Guid userId, string userName, string userRole)
    {
        if (_participants.Any(p => p.UserId == userId))
            return;

        _participants.Add(new ChatParticipant(Id, userId, userName, userRole));
        UpdatedAt = DateTime.UtcNow;
    }

    public ChatMessage AddMessage(Guid senderId, string senderName, string senderRole, string content)
    {
        var message = new ChatMessage(Id, senderId, senderName, senderRole, content);
        _messages.Add(message);
        UpdatedAt = DateTime.UtcNow;
        return message;
    }
}
