using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.Application.Common.Security;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;

namespace SmartHotel.Identity.API.Controllers;

// ── Request / Response DTOs ──────────────────────────────────────────────────

public class CreateEmployeeRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    /// <summary>Login email (unique across all persons).</summary>
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    /// <summary>Contact/notification email if different from login email.</summary>
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    /// <summary>
    /// Role requested by Admin. Ignored when requester is a Manager (forced to Receptionist).
    /// Valid Admin values: Manager | Receptionist | Housekeeper | Maintenance | Chef | Waiter | Security
    /// </summary>
    public string? Role { get; set; }
    public Guid DepartmentId { get; set; }
    public string? Designation { get; set; }
}

public class ApproveEmployeeRequest { /* intentionally empty – no body required */ }

public class RejectEmployeeRequest
{
    public string? Reason { get; set; }
}

public class UpdateEmployeeRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public class UpdateEmployeeDepartmentRequest
{
    public Guid DepartmentId { get; set; }
}

public class UpdateEmployeeProfileRequest
{
    public string? NationalId { get; set; }
    public string? NicPhotoUrl { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranch { get; set; }
}

public class SuspendEmployeeRequest { public string Reason { get; set; } = string.Empty; }

public class EmployeeProfileDto : EmployeeListDto
{
    public string? NationalId { get; set; }
    public string? NicPhotoUrl { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranch { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public bool ProfileCompletionRequired { get; set; }
    public DateTime? ProfileCompletionDeadlineUtc { get; set; }
}

public class EmployeeListDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? Designation { get; set; }
    public Guid DepartmentId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedByName { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    /// <summary>Returned only once when credentials are generated or regenerated.</summary>
    public string? TemporaryPassword { get; set; }
}

// ── Controller ───────────────────────────────────────────────────────────────

[ApiController]
[Produces("application/json")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public EmployeesController(AppDbContext context, IPasswordHasher passwordHasher, IEmailService emailService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string? GetRequesterRole() =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;

    private Guid? GetRequesterId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private Guid? GetRequesterDepartmentId()
    {
        var value = User.FindFirst("departmentId")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static EmployeeListDto ToDto(Employee e, string? createdByName = null) => new()
    {
        Id = e.Id,
        FirstName = e.FirstName,
        LastName = e.LastName,
        FullName = $"{e.FirstName} {e.LastName}".Trim(),
        Email = e.Email,
        Role = e.Role.ToString(),
        Status = e.IsActive ? e.Status.ToString() : "Suspended",
        DepartmentName = e.Department?.Name,
        Designation = e.Designation,
        DepartmentId = e.DepartmentId,
        IsActive = e.IsActive,
        CreatedAtUtc = e.CreatedAtUtc,
        CreatedByName = createdByName,
        RejectionReason = e.RejectionReason,
        ReviewedAtUtc = e.ReviewedAtUtc
    };

    private static EmployeeProfileDto ToProfileDto(Employee e) => new()
    {
        Id = e.Id, FirstName = e.FirstName, LastName = e.LastName,
        FullName = $"{e.FirstName} {e.LastName}".Trim(), Email = e.Email,
        Role = e.Role.ToString(), Status = e.IsActive ? e.Status.ToString() : "Suspended",
        DepartmentName = e.Department?.Name, DepartmentId = e.DepartmentId,
        IsActive = e.IsActive, CreatedAtUtc = e.CreatedAtUtc,
        NationalId = e.NationalId, NicPhotoUrl = e.NicPhotoUrl, ProfilePhotoUrl = e.ProfilePhotoUrl,
        ProfileCompletionRequired = e.RequiresProfileCompletion && !e.HasCompletedRequiredProfile(),
        ProfileCompletionDeadlineUtc = e.ProfileCompletionDeadlineUtc,
        BankName = e.BankName, BankAccountName = e.BankAccountName,
        BankAccountNumber = e.BankAccountNumber, BankBranch = e.BankBranch,
        SuspensionReason = e.SuspensionReason, SuspendedAtUtc = e.SuspendedAtUtc
    };

    private bool CanManage(Employee target)
    {
        var role = GetRequesterRole();
        return role == "Owner"
            || role == "Admin" && GetRequesterDepartmentId() == target.DepartmentId && target.Role != EmployeeRole.Owner;
    }

    // ── GET /api/v1/employees ─────────────────────────────────────────────────

    /// <summary>
    /// List all employees. Admin sees everyone; Manager sees Active employees only.
    /// </summary>
    [HttpGet("api/v1/employees")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var requesterRole = GetRequesterRole();
        if (requesterRole is not ("Owner" or "Admin" or "Manager"))
            return Forbid();

        var query = _context.Employees
            .Include(e => e.Department)
            .AsQueryable();

        if (requesterRole is "Admin" or "Manager")
        {
            var departmentId = GetRequesterDepartmentId();
            if (!departmentId.HasValue) return Forbid();
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }
        if (requesterRole == "Manager") query = query.Where(e => e.Status == EmployeeStatus.Active);

        var employees = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);

        // Resolve createdBy names in bulk
        var creatorIds = employees
            .Where(e => e.CreatedByEmployeeId.HasValue)
            .Select(e => e.CreatedByEmployeeId!.Value)
            .Distinct()
            .ToList();

        var creatorNames = creatorIds.Count > 0
            ? await _context.Employees
                .Where(e => creatorIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => $"{e.FirstName} {e.LastName}".Trim(), ct)
            : new Dictionary<Guid, string>();

        var dtos = employees.Select(e =>
        {
            var name = e.CreatedByEmployeeId.HasValue && creatorNames.TryGetValue(e.CreatedByEmployeeId.Value, out var n) ? n : null;
            return ToDto(e, name);
        });

        return Ok(dtos);
    }

    // ── GET /api/v1/employees/pending ─────────────────────────────────────────

    /// <summary>
    /// Admin-only: list all employees with Status = PendingApproval.
    /// </summary>
    [HttpGet("api/v1/employees/pending")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        if (GetRequesterRole() != "Admin")
            return Forbid();

        var requesterDepartmentId = GetRequesterDepartmentId();
        if (!requesterDepartmentId.HasValue) return Forbid();

        var pending = await _context.Employees
            .Include(e => e.Department)
            .Where(e => e.Status == EmployeeStatus.PendingApproval && e.DepartmentId == requesterDepartmentId.Value)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(ct);

        var creatorIds = pending
            .Where(e => e.CreatedByEmployeeId.HasValue)
            .Select(e => e.CreatedByEmployeeId!.Value)
            .Distinct()
            .ToList();

        var creatorNames = creatorIds.Count > 0
            ? await _context.Employees
                .Where(e => creatorIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => $"{e.FirstName} {e.LastName}".Trim(), ct)
            : new Dictionary<Guid, string>();

        var dtos = pending.Select(e =>
        {
            var name = e.CreatedByEmployeeId.HasValue && creatorNames.TryGetValue(e.CreatedByEmployeeId.Value, out var n) ? n : null;
            return ToDto(e, name);
        });

        return Ok(dtos);
    }

    // ── POST /api/v1/employees ────────────────────────────────────────────────

    /// <summary>
    /// Create a new employee.
    /// Admin → can create any role (Manager, Staff, etc.) → Status = Active immediately.
    /// Manager → can only create non-Manager roles → Status = PendingApproval.
    /// Other roles → 403.
    /// </summary>
    [HttpPost("api/v1/employees")]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var requesterRole = GetRequesterRole();
        var requesterId = GetRequesterId();

        if (!Enum.TryParse<EmployeeRole>(requesterRole, ignoreCase: true, out var creatorRole) ||
            creatorRole is not (EmployeeRole.Owner or EmployeeRole.Admin or EmployeeRole.Manager))
            return Forbid();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            request.DepartmentId == Guid.Empty)
        {
            return BadRequest(new { message = "FirstName, LastName, Email, and DepartmentId are required." });
        }

        var targetDepartment = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DepartmentId, ct);
        if (targetDepartment is null)
            return BadRequest(new { message = $"Department with ID {request.DepartmentId} does not exist." });

        if (!string.IsNullOrWhiteSpace(request.Designation))
        {
            var allowedFinanceDesignations = new[] { "Accountant", "Cashier", "Finance Assistant" };
            if (!string.Equals(targetDepartment.Name, "Finance", StringComparison.OrdinalIgnoreCase) ||
                !allowedFinanceDesignations.Contains(request.Designation.Trim(), StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = "Designation must be Accountant, Cashier, or Finance Assistant and is valid only for Finance employees." });
        }

        if (creatorRole is EmployeeRole.Admin or EmployeeRole.Manager)
        {
            var requesterDepartmentId = GetRequesterDepartmentId();
            if (!requesterDepartmentId.HasValue || request.DepartmentId != requesterDepartmentId.Value)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Staff can be created only in your assigned department." });
        }

        if (creatorRole == EmployeeRole.Manager)
        {
            if (!requesterId.HasValue)
                return Forbid();

            var manager = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == requesterId.Value, ct);

            if (manager is null || manager.Status != EmployeeStatus.Active || !manager.IsActive || manager.DepartmentId == Guid.Empty)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Manager does not have an active department assignment." });

            var managesDepartment = await _context.Departments
                .AnyAsync(d => d.Id == manager.DepartmentId && d.ManagerId == manager.Id, ct);

            if (!managesDepartment)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Manager is not assigned as the manager of an active department." });

            if (request.DepartmentId != manager.DepartmentId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Managers can create employees only in their assigned department." });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Unique email check across all persons
        var emailTaken = await _context.Persons
            .AnyAsync(p => p.Email.ToLower() == normalizedEmail, ct);
        if (emailTaken)
            return Conflict(new { message = "An account with this email address already exists." });

        EmployeeRole? assignedRole = string.IsNullOrWhiteSpace(request.Role)
            ? EmployeeRoleHierarchy.DefaultCreatedRole(creatorRole)
            : Enum.TryParse<EmployeeRole>(request.Role, ignoreCase: true, out var requestedRole)
                ? requestedRole
                : null;

        if (!assignedRole.HasValue || !EmployeeRoleHierarchy.CanCreate(creatorRole, assignedRole.Value))
            return Forbid();

        var assignedStatus = creatorRole == EmployeeRole.Manager
            ? EmployeeStatus.PendingApproval
            : EmployeeStatus.Active;

        var generatedPassword = PasswordGenerator.Generate();

        var employee = new Employee
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail,
            ContactEmail = !string.IsNullOrWhiteSpace(request.ContactEmail) ? request.ContactEmail.Trim() : null,
            PasswordHash = _passwordHasher.HashPassword(generatedPassword),
            Role = assignedRole.Value,
            Status = assignedStatus,
            IsActive = assignedStatus == EmployeeStatus.Active,
            EmailVerified = true, // Admin/Manager-provisioned accounts skip email verification
            MustChangePassword = true, // Force password change on first login
            DepartmentId = request.DepartmentId,
            Designation = request.Designation?.Trim(),
            CreatedByEmployeeId = requesterId
            ,RequiresProfileCompletion = creatorRole == EmployeeRole.Owner && assignedRole.Value == EmployeeRole.Admin
        };

        _context.Employees.Add(employee);
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateException) when (assignedRole is EmployeeRole.Admin or EmployeeRole.Manager)
        {
            return Conflict(new { message = $"This department already has an active {assignedRole}." });
        }

        // Reload with department navigation
        await _context.Entry(employee).Reference(e => e.Department).LoadAsync(ct);

        // Deliver credentials via email immediately if Admin created (Active)
        if (assignedStatus == EmployeeStatus.Active)
        {
            await _emailService.SendWelcomeWithCredentialsAsync(
                employee.Email,
                $"{employee.FirstName} {employee.LastName}".Trim(),
                generatedPassword,
                ct);
        }

        var dto = ToDto(employee);
        dto.TemporaryPassword = generatedPassword;
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>
    /// Owner-only recovery action: invalidate the previous password, generate a new
    /// temporary password, require a first-login reset, and resend the welcome email.
    /// The temporary password is also returned once so the owner can hand it over
    /// securely if the recipient's mail provider delays or filters the message.
    /// </summary>
    [HttpPost("api/v1/employees/{id:guid}/resend-credentials")]
    public async Task<IActionResult> ResendCredentials([FromRoute] Guid id, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null)
            return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee)) return Forbid();

        if (!employee.IsActive || employee.Status != EmployeeStatus.Active)
            return BadRequest(new { message = "Credentials can only be resent for an active account." });

        var generatedPassword = PasswordGenerator.Generate();
        employee.PasswordHash = _passwordHasher.HashPassword(generatedPassword);
        employee.MustChangePassword = true;
        await _context.SaveChangesAsync(ct);

        await _emailService.SendWelcomeWithCredentialsAsync(
            employee.Email,
            $"{employee.FirstName} {employee.LastName}".Trim(),
            generatedPassword,
            ct);

        return Ok(new
        {
            message = $"New credentials were generated and sent to {employee.Email}.",
            temporaryPassword = generatedPassword
        });
    }

    [HttpGet("api/v1/employees/{id:guid}/profile")]
    public async Task<IActionResult> GetProfile([FromRoute] Guid id, CancellationToken ct)
    {
        var employee = await _context.Employees.Include(e => e.Department).AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee)) return Forbid();
        return Ok(ToProfileDto(employee));
    }

    [HttpGet("api/v1/employees/me/profile")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var id = GetRequesterId();
        if (!id.HasValue) return Unauthorized();
        var employee = await _context.Employees.Include(e => e.Department).AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return employee is null ? NotFound(new { message = "Employee not found." }) : Ok(ToProfileDto(employee));
    }

    [HttpPut("api/v1/employees/me/profile")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateEmployeeProfileRequest request, CancellationToken ct)
    {
        var id = GetRequesterId();
        if (!id.HasValue) return Unauthorized();
        var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return NotFound(new { message = "Employee not found." });
        if (employee.ProfileCompletionDeadlineUtc <= DateTime.UtcNow && !employee.HasCompletedRequiredProfile())
        {
            employee.IsActive = false; employee.SuspensionReason = "Required profile details were not completed within two days."; employee.SuspendedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return StatusCode(403, new { message = "The two-day profile completion deadline has expired. Please contact the owner." });
        }
        employee.NationalId = request.NationalId?.Trim(); employee.NicPhotoUrl = request.NicPhotoUrl?.Trim(); employee.ProfilePhotoUrl = request.ProfilePhotoUrl?.Trim();
        employee.BankName = request.BankName?.Trim(); employee.BankAccountName = request.BankAccountName?.Trim(); employee.BankAccountNumber = request.BankAccountNumber?.Trim(); employee.BankBranch = request.BankBranch?.Trim();
        if (!employee.HasCompletedRequiredProfile())
            return BadRequest(new { message = "Profile picture, NIC photo, NIC number and all bank details are required." });
        employee.ProfileCompletedAtUtc = DateTime.UtcNow;
        employee.RequiresProfileCompletion = false;
        await _context.SaveChangesAsync(ct);
        var token = HttpContext.RequestServices.GetRequiredService<IJwtTokenService>().GenerateAccessToken(employee);
        return Ok(new { message = "Profile completed. Portal access is now enabled.", profile = ToProfileDto(employee), accessToken = token, profileComplete = true });
    }

    [HttpPut("api/v1/employees/{id:guid}/profile")]
    public async Task<IActionResult> UpdateProfile([FromRoute] Guid id, [FromBody] UpdateEmployeeProfileRequest request, CancellationToken ct)
    {
        var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee)) return Forbid();
        employee.NationalId = request.NationalId?.Trim(); employee.NicPhotoUrl = request.NicPhotoUrl?.Trim(); employee.ProfilePhotoUrl = request.ProfilePhotoUrl?.Trim();
        employee.BankName = request.BankName?.Trim(); employee.BankAccountName = request.BankAccountName?.Trim();
        employee.BankAccountNumber = request.BankAccountNumber?.Trim(); employee.BankBranch = request.BankBranch?.Trim();
        await _context.SaveChangesAsync(ct);
        return Ok(ToProfileDto(employee));
    }

    [HttpPost("api/v1/employees/{id:guid}/suspend")]
    public async Task<IActionResult> Suspend([FromRoute] Guid id, [FromBody] SuspendEmployeeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { message = "Suspension reason is required." });
        var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee) || GetRequesterId() == employee.Id) return Forbid();
        employee.IsActive = false; employee.SuspensionReason = request.Reason.Trim(); employee.SuspendedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Ok(ToProfileDto(employee));
    }

    [HttpPost("api/v1/employees/{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate([FromRoute] Guid id, CancellationToken ct)
    {
        var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee)) return Forbid();
        employee.IsActive = true; employee.Status = EmployeeStatus.Active; employee.SuspensionReason = null; employee.SuspendedAtUtc = null;
        await _context.SaveChangesAsync(ct);
        return Ok(ToProfileDto(employee));
    }

    [HttpDelete("api/v1/employees/{id:guid}")]
    public async Task<IActionResult> DeleteEmployee([FromRoute] Guid id, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee) || GetRequesterId() == employee.Id) return Forbid();
        employee.IsActive = false;
        employee.Status = EmployeeStatus.Rejected;
        employee.SuspensionReason = "Account deactivated; record retained for audit history.";
        employee.SuspendedAtUtc = DateTime.UtcNow;
        employee.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── POST /api/v1/employees/{id}/approve ───────────────────────────────────

    /// <summary>
    /// Admin-only: approve a PendingApproval employee → Status = Active.
    /// </summary>
    [HttpPost("api/v1/employees/{id:guid}/approve")]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve([FromRoute] Guid id, CancellationToken ct)
    {
        if (GetRequesterRole() != "Admin")
            return Forbid();

        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (employee == null)
            return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee)) return Forbid();

        if (employee.Status != EmployeeStatus.PendingApproval)
            return BadRequest(new { message = $"Employee is not pending approval (current status: {employee.Status})." });

        employee.Status = EmployeeStatus.Active;
        employee.IsActive = true;
        employee.ReviewedByEmployeeId = GetRequesterId();
        employee.ReviewedAtUtc = DateTime.UtcNow;
        employee.UpdatedAtUtc = DateTime.UtcNow;

        // Generate fresh temporary password upon approval & enforce password reset on first login
        var tempPassword = PasswordGenerator.Generate();
        employee.PasswordHash = _passwordHasher.HashPassword(tempPassword);
        employee.MustChangePassword = true;

        await _context.SaveChangesAsync(ct);

        // Deliver credentials via email
        await _emailService.SendWelcomeWithCredentialsAsync(
            employee.Email,
            $"{employee.FirstName} {employee.LastName}".Trim(),
            tempPassword,
            ct);

        return Ok(ToDto(employee));
    }

    // ── POST /api/v1/employees/{id}/reject ────────────────────────────────────

    /// <summary>
    /// Admin-only: reject a PendingApproval employee → Status = Rejected (record kept for audit).
    /// </summary>
    [HttpPost("api/v1/employees/{id:guid}/reject")]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] RejectEmployeeRequest? request, CancellationToken ct)
    {
        if (GetRequesterRole() != "Admin")
            return Forbid();

        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (employee == null)
            return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee)) return Forbid();

        if (employee.Status != EmployeeStatus.PendingApproval)
            return BadRequest(new { message = $"Employee is not pending approval (current status: {employee.Status})." });

        employee.Status = EmployeeStatus.Rejected;
        employee.IsActive = false;
        employee.ReviewedByEmployeeId = GetRequesterId();
        employee.ReviewedAtUtc = DateTime.UtcNow;
        employee.RejectionReason = request?.Reason?.Trim();
        employee.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Ok(ToDto(employee));
    }

    [HttpPost("api/v1/employees/{id:guid}/revoke")]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Revoke([FromRoute] Guid id, [FromBody] RejectEmployeeRequest? request, CancellationToken ct)
    {
        if (GetRequesterRole() != "Admin")
            return Forbid();

        var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null)
            return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee)) return Forbid();
        if (employee.Role is EmployeeRole.Owner or EmployeeRole.Admin or EmployeeRole.Manager)
            return BadRequest(new { message = "This endpoint revokes approved Employee-role accounts only." });
        if (employee.Status != EmployeeStatus.Active)
            return BadRequest(new { message = $"Employee is not approved (current status: {employee.Status})." });

        employee.Status = EmployeeStatus.Rejected;
        employee.IsActive = false;
        employee.ReviewedByEmployeeId = GetRequesterId();
        employee.ReviewedAtUtc = DateTime.UtcNow;
        employee.RejectionReason = request?.Reason?.Trim();
        employee.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(employee));
    }

    // ── PUT /api/v1/employees/{id}/role ───────────────────────────────────────

    /// <summary>
    /// Admin-only: update an employee's role.
    /// </summary>
    [HttpPut("api/v1/employees/{id:guid}/role")]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRole([FromRoute] Guid id, [FromBody] UpdateEmployeeRoleRequest request, CancellationToken ct)
    {
        if (GetRequesterRole() != "Admin")
            return Forbid();

        if (string.IsNullOrWhiteSpace(request?.Role) ||
            !Enum.TryParse<EmployeeRole>(request.Role, ignoreCase: true, out var newRole))
        {
            return BadRequest(new { message = $"Invalid employee role '{request?.Role}'." });
        }

        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (employee == null)
            return NotFound(new { message = "Employee not found." });
        if (!CanManage(employee)) return Forbid();

        if (employee.Role == EmployeeRole.Manager && newRole != EmployeeRole.Manager &&
            await _context.Departments.AnyAsync(d => d.ManagerId == employee.Id, ct))
        {
            return BadRequest(new { message = "Remove this employee as department manager before changing their Manager role." });
        }

        employee.Role = newRole;
        employee.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Ok(ToDto(employee));
    }

    [HttpPut("api/v1/employees/{id:guid}/department")]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateDepartment([FromRoute] Guid id, [FromBody] UpdateEmployeeDepartmentRequest request, CancellationToken ct)
    {
        if (GetRequesterRole() is not ("Owner" or "Admin")) return Forbid();
        if (request.DepartmentId == Guid.Empty || !await _context.Departments.AnyAsync(d => d.Id == request.DepartmentId, ct))
            return BadRequest(new { message = $"Department with ID {request.DepartmentId} does not exist." });

        var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return NotFound(new { message = "Employee not found." });
        if (GetRequesterRole() == "Admin" &&
            (GetRequesterDepartmentId() != employee.DepartmentId || GetRequesterDepartmentId() != request.DepartmentId))
            return Forbid();

        var managedDepartment = await _context.Departments.FirstOrDefaultAsync(d => d.ManagerId == id, ct);
        if (managedDepartment is not null && managedDepartment.Id != request.DepartmentId)
            return BadRequest(new { message = "A department manager cannot be moved outside the department they manage." });

        employee.DepartmentId = request.DepartmentId;
        employee.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _context.Entry(employee).Reference(e => e.Department).LoadAsync(ct);
        return Ok(ToDto(employee));
    }
}
