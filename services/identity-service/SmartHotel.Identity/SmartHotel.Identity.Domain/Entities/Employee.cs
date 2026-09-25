using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.Domain.Entities;

public class Employee : Person
{
    public EmployeeRole Role { get; set; } = EmployeeRole.Receptionist;

    public Guid DepartmentId { get; set; } = Guid.Parse("22222222-2222-2222-2222-222222222220");
    public Department? Department { get; set; }
    public string? Designation { get; set; }

    // Populated by AppDbContext for active department Admin/Manager accounts.
    // A unique index on this nullable value provides concurrency-safe role slots.
    public string? DepartmentRoleSlot { get; set; }

    public bool MustChangePassword { get; set; } = false;

    // ── Approval workflow ─────────────────────────────────────────────────────

    /// <summary>
    /// Approval lifecycle status. Active = can log in. PendingApproval / Rejected = login blocked.
    /// </summary>
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>
    /// Id of the Employee (Admin or Manager) who created this record.
    /// </summary>
    public Guid? CreatedByEmployeeId { get; set; }

    /// <summary>
    /// Id of the Admin who approved or rejected this record.
    /// </summary>
    public Guid? ReviewedByEmployeeId { get; set; }

    /// <summary>
    /// UTC timestamp when an Admin approved or rejected this record.
    /// </summary>
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>
    /// Optional reason provided by Admin when rejecting an employee.
    /// </summary>
    public string? RejectionReason { get; set; }

    public string? NationalId { get; set; }
    public string? NicPhotoUrl { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranch { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public bool RequiresProfileCompletion { get; set; }
    public DateTime? ProfileCompletionDeadlineUtc { get; set; }
    public DateTime? ProfileCompletedAtUtc { get; set; }

    public bool HasCompletedRequiredProfile() =>
        !string.IsNullOrWhiteSpace(ProfilePhotoUrl) &&
        !string.IsNullOrWhiteSpace(NicPhotoUrl) &&
        !string.IsNullOrWhiteSpace(NationalId) &&
        !string.IsNullOrWhiteSpace(BankName) &&
        !string.IsNullOrWhiteSpace(BankAccountName) &&
        !string.IsNullOrWhiteSpace(BankAccountNumber) &&
        !string.IsNullOrWhiteSpace(BankBranch);
}
