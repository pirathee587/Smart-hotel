namespace SmartHotel.Notifications.Domain;

public enum ManagerAlertStatus
{
    Open,
    Dismissed,
    Actioned
}

public class ManagerAlert
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EventId { get; private set; }
    public string AlertType { get; private set; } = string.Empty;
    public string Severity { get; private set; } = string.Empty;
    public Guid? GuestId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid? BookingId { get; private set; }
    public string? BookingReference { get; private set; }
    public Guid? TaskId { get; private set; }
    public string MessageSnippet { get; private set; } = string.Empty;
    public string? PayloadJson { get; private set; }
    public ManagerAlertStatus Status { get; private set; } = ManagerAlertStatus.Open;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DismissedAt { get; private set; }
    public Guid? DismissedBy { get; private set; }
    public DateTime? ActionedAt { get; private set; }
    public Guid? ActionedBy { get; private set; }

    private ManagerAlert() { }

    public ManagerAlert(
        Guid eventId,
        string alertType,
        string severity,
        string messageSnippet,
        Guid? guestId = null,
        Guid? roomId = null,
        Guid? bookingId = null,
        string? bookingReference = null,
        Guid? taskId = null,
        string? payloadJson = null,
        DateTime? createdAt = null)
    {
        EventId = eventId;
        AlertType = alertType ?? throw new ArgumentNullException(nameof(alertType));
        Severity = severity ?? throw new ArgumentNullException(nameof(severity));
        MessageSnippet = messageSnippet ?? throw new ArgumentNullException(nameof(messageSnippet));
        GuestId = guestId;
        RoomId = roomId;
        BookingId = bookingId;
        BookingReference = bookingReference;
        TaskId = taskId;
        PayloadJson = payloadJson;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }

    public void Dismiss(Guid actorId)
    {
        Status = ManagerAlertStatus.Dismissed;
        DismissedBy = actorId;
        DismissedAt = DateTime.UtcNow;
    }

    public void MarkActioned(Guid actorId)
    {
        Status = ManagerAlertStatus.Actioned;
        ActionedBy = actorId;
        ActionedAt = DateTime.UtcNow;
    }
}
