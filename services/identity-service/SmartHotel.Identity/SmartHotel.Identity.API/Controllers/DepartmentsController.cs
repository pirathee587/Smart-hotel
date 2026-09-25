using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;

namespace SmartHotel.Identity.API.Controllers;

public sealed record DepartmentEmployeeDto(Guid Id, string FullName, string Email, string Role);
public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string Description,
    Guid? ManagerId,
    string? ManagerName,
    int EmployeeCount,
    IReadOnlyList<DepartmentEmployeeDto> Employees);
public sealed record SaveDepartmentRequest(string Name, string? Description, Guid? ManagerId);

[ApiController]
[Route("api/v1/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DepartmentsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(CancellationToken ct)
    {
        var role = GetRole();
        var query = _context.Departments
            .AsNoTracking()
            .Include(d => d.Manager)
            .Include(d => d.Employees)
            .AsQueryable();

        if (role != "Owner")
        {
            if (role is not ("Admin" or "Manager") || !GetDepartmentId().HasValue) return Forbid();
            var departmentId = GetDepartmentId()!.Value;
            query = query.Where(d => d.Id == departmentId);
        }

        var departments = await query.OrderBy(d => d.Name).ToListAsync(ct);

        return Ok(departments.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(SaveDepartmentRequest request, CancellationToken ct)
    {
        if (GetRole() != "Owner") return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Department name is required." });
        var normalizedName = request.Name.Trim().ToLower();
        if (await _context.Departments.AnyAsync(d => d.Name.ToLower() == normalizedName, ct))
            return Conflict(new { message = "A department with that name already exists." });

        var department = new Department
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty
        };
        _context.Departments.Add(department);

        var validationError = await AssignManager(department, request.ManagerId, ct);
        if (validationError is not null) return BadRequest(new { message = validationError });

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "That manager is already assigned to another department." });
        }
        return CreatedAtAction(nameof(GetAll), new { id = department.Id }, ToDto(department));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, SaveDepartmentRequest request, CancellationToken ct)
    {
        if (!CanManageDepartment(id)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Department name is required." });

        var department = await _context.Departments
            .Include(d => d.Manager)
            .Include(d => d.Employees)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (department is null) return NotFound(new { message = "Department not found." });

        var validationError = await AssignManager(department, request.ManagerId, ct);
        if (validationError is not null) return BadRequest(new { message = validationError });

        department.Name = request.Name.Trim();
        department.Description = request.Description?.Trim() ?? string.Empty;
        department.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "That manager is already assigned to another department." });
        }
        return Ok(ToDto(department));
    }

    private async Task<string?> AssignManager(Department department, Guid? managerId, CancellationToken ct)
    {
        if (!managerId.HasValue)
        {
            department.ManagerId = null;
            department.Manager = null;
            return null;
        }

        var manager = await _context.Employees.FirstOrDefaultAsync(e => e.Id == managerId.Value, ct);
        if (manager is null) return $"Employee with ID {managerId.Value} does not exist.";
        if (manager.Role != EmployeeRole.Manager) return $"Employee {manager.FirstName} {manager.LastName} must have the Manager role.";

        var otherDepartment = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ManagerId == manager.Id && d.Id != department.Id, ct);
        if (otherDepartment is not null)
            return $"{manager.FirstName} {manager.LastName} already manages department '{otherDepartment.Name}'.";

        manager.DepartmentId = department.Id;
        manager.UpdatedAtUtc = DateTime.UtcNow;
        department.ManagerId = manager.Id;
        department.Manager = manager;
        return null;
    }

    private string? GetRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;

    private Guid? GetDepartmentId() =>
        Guid.TryParse(User.FindFirst("departmentId")?.Value, out var id) ? id : null;

    private bool CanManageDepartment(Guid departmentId) =>
        GetRole() == "Owner" || GetRole() == "Admin" && GetDepartmentId() == departmentId;

    private static DepartmentDto ToDto(Department department) => new(
        department.Id,
        department.Name,
        department.Description,
        department.ManagerId,
        department.Manager is null ? null : $"{department.Manager.FirstName} {department.Manager.LastName}".Trim(),
        department.Employees.Count,
        department.Employees
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new DepartmentEmployeeDto(e.Id, $"{e.FirstName} {e.LastName}".Trim(), e.Email, e.Role.ToString()))
            .ToList());
}
