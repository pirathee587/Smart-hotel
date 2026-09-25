namespace SmartHotel.Identity.Domain.Enums;

/// <summary>
/// Approval lifecycle status of an Employee record.
/// </summary>
public enum EmployeeStatus
{
    /// <summary>
    /// Employee is active and may authenticate.
    /// Set when Admin creates any employee, or when Admin approves a Manager-created employee.
    /// </summary>
    Active,

    /// <summary>
    /// Employee was created by a Manager and is waiting for Admin approval.
    /// Login is blocked while in this state.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Admin has reviewed and rejected this employee.
    /// Record is kept for audit trail; login is blocked.
    /// </summary>
    Rejected
}
