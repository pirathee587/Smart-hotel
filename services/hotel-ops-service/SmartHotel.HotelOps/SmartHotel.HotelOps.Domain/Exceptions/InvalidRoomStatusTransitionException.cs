namespace SmartHotel.HotelOps.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class InvalidRoomStatusTransitionException : DomainException
{
    public Enums.RoomStatus CurrentStatus { get; }
    public Enums.RoomStatus AttemptedStatus { get; }

    public InvalidRoomStatusTransitionException(Enums.RoomStatus currentStatus, Enums.RoomStatus attemptedStatus, string reason)
        : base($"Cannot transition room from {currentStatus} to {attemptedStatus}. Reason: {reason}")
    {
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}
