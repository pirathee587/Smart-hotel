namespace SmartHotel.Booking.Domain.Enums;

public enum BookingStatus
{
    PendingPayment = 0,
    Confirmed = 1,
    CheckedIn = 2,
    CheckedOut = 3,
    Cancelled = 4
}

public enum PaymentStatus
{
    Created = 0,
    Completed = 1,
    Refunded = 2,
    Failed = 3
}

public enum PaymentProvider
{
    PayHere = 1
}

public enum ComplaintSeverity
{
    Low = 1,      // 48 hours SLA
    Medium = 2,   // 24 hours SLA
    High = 3,     // 6 hours SLA
    Critical = 4  // 2 hours SLA - Routes directly to Admin
}

public enum ComplaintStatus
{
    Open = 1,
    InProgress = 2,
    Escalated = 3,
    Resolved = 4,
    Closed = 5,
    Reopened = 6
}

public enum ComplaintCategory
{
    Cleanliness = 1,
    Noise = 2,
    Maintenance = 3,
    Billing = 4,
    Staff = 5,
    Other = 6
}
