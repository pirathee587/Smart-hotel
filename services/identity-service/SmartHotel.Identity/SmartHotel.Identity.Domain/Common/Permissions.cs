namespace SmartHotel.Identity.Domain.Common;

public static class RolePermissions
{
    // Guest permissions
    public const string ViewRooms = "rooms.view";
    public const string BookRoom = "bookings.create";
    public const string ViewOwnBookings = "bookings.view_own";
    public const string ManageOwnProfile = "profile.manage";

    // Employee permissions
    public const string ViewAssignedTasks = "tasks.view_assigned";
    public const string UpdateTaskStatus = "tasks.update";
    public const string GuestCheckInCheckOut = "guests.checkin_checkout";

    // Manager permissions
    public const string ManageBookings = "bookings.manage_all";
    public const string ManageRooms = "rooms.manage";
    public const string ViewReports = "reports.view";
    public const string AssignTasks = "tasks.assign";

    // Admin permissions
    public const string ManageUsers = "users.manage";
    public const string ManageRoles = "roles.manage";
    public const string SystemSettings = "system.settings";
    public const string ViewAuditLogs = "audit.view";

    public static IReadOnlyList<string> GetPermissionsForRole(string role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "owner" => new[]
            {
                ViewRooms, BookRoom, ViewOwnBookings, ManageOwnProfile,
                ViewAssignedTasks, UpdateTaskStatus, GuestCheckInCheckOut,
                ManageBookings, ManageRooms, ViewReports, AssignTasks,
                ManageUsers, ManageRoles, SystemSettings, ViewAuditLogs
            },
            "admin" => new[]
            {
                ViewRooms, ManageOwnProfile, ViewAssignedTasks, UpdateTaskStatus,
                GuestCheckInCheckOut, ManageBookings, ManageRooms, ViewReports,
                AssignTasks, ManageUsers, ViewAuditLogs
            },
            "manager" => new[]
            {
                ViewRooms, ManageOwnProfile,
                ViewAssignedTasks, UpdateTaskStatus, GuestCheckInCheckOut,
                ManageBookings, ManageRooms, ViewReports, AssignTasks
            },
            "receptionist" => new[]
            {
                ViewRooms, ManageOwnProfile, ViewAssignedTasks, UpdateTaskStatus,
                GuestCheckInCheckOut, ManageBookings
            },
            "housekeeper" or "maintenance" or "chef" or "waiter" or "security" or "employee" => new[]
            {
                ViewRooms, ManageOwnProfile, ViewAssignedTasks, UpdateTaskStatus
            },
            "guest" or "customer" => new[]
            {
                ViewRooms, BookRoom, ViewOwnBookings, ManageOwnProfile
            },
            _ => Array.Empty<string>()
        };
    }
}
