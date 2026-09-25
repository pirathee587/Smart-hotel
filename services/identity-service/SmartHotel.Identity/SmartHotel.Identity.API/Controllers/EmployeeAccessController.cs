using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;

namespace SmartHotel.Identity.API.Controllers;

[ApiController]
public sealed class EmployeeAccessController : ControllerBase
{
    private readonly AppDbContext _context;
    public EmployeeAccessController(AppDbContext context) => _context = context;

    [HttpGet("api/v1/internal/employees/{id:guid}/access-status")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var employee = await _context.Employees.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { approved = e.IsActive && e.Status == EmployeeStatus.Active, departmentId = e.DepartmentId })
            .FirstOrDefaultAsync(ct);
        return employee is null ? NotFound() : Ok(employee);
    }
}
