namespace SmartHotel.Notifications.Domain;

public class Notification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public NotificationType Type { get; private set; } = NotificationType.System;
    public string? PayloadJson { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; private set; }

    private Notification() { } // EF Core

    public Notification(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? payloadJson = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Type = type;
        PayloadJson = payloadJson;
        IsRead = false;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
        }
    }
}
