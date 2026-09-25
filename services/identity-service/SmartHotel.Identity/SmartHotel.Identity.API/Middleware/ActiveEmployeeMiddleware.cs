using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;

namespace SmartHotel.Identity.API.Middleware;

public sealed class ActiveEmployeeMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveEmployeeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? context.User.FindFirst("role")?.Value;
            var subject = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;
            if (IsEmployeeRole(role) && Guid.TryParse(subject, out var employeeId))
            {
                var approved = await dbContext.Employees.AsNoTracking()
                    .AnyAsync(e => e.Id == employeeId && e.IsActive && e.Status == EmployeeStatus.Active, context.RequestAborted);
                if (!approved)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "employee_not_approved", message = "Employee account is not approved for access." });
                    return;
                }
            }
        }

        await _next(context);
    }

    private static bool IsEmployeeRole(string? role) => role is
        "Owner" or "Admin" or "Manager" or "Receptionist" or "Housekeeper" or "Maintenance" or "Chef" or "Waiter" or "Security";
}
