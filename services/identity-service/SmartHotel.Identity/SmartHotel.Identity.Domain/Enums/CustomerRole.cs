namespace SmartHotel.Identity.Domain.Enums;

/// <summary>
/// Defines the role of a customer in the SmartHotel system.
/// Used for permission gating across the application.
/// </summary>
public enum CustomerRole
{
    /// <summary>Standard hotel guest — default role for all new registrations.</summary>
    Guest,

    /// <summary>Hotel administrator — full access to all hotel management features.</summary>
    Admin,

    /// <summary>Hotel manager — access to reports, bookings, and staff management.</summary>
    Manager,

    /// <summary>Hotel employee — limited operational access (front desk, housekeeping, etc.).</summary>
    Employee
}
