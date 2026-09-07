namespace SmartHotel.Notifications.Domain;

public enum NotificationType
{
    TaskAssigned,
    RoomStatusUpdated,
    DeliveryAlert,
    BookingCreated,
    PaymentRefunded,
    ChatMessage,
    System
}

public enum ChatRoomType
{
    Direct,
    Group
}
