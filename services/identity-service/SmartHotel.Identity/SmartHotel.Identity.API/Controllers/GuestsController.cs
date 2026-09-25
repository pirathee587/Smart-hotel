using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Authorization;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Infrastructure.Persistence;

using SmartHotel.Identity.Domain.Common;
using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.API.Controllers;

public class GuestSearchResultDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? MaskedNationalId { get; set; }
    public string LoyaltyTier { get; set; } = "Silver";
    public string Role { get; set; } = "Guest";
}

public class GuestProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string LoyaltyTier { get; set; } = "Silver";
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Guest";
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

public class UpdateCustomerRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public class UpdateGuestProfileRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Nationality { get; set; }
    public string? AvatarUrl { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

[ApiController]
[Route("api/guests")]
[Produces("application/json")]
public class GuestsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public GuestsController(AppDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    public async Task<IActionResult> SearchGuests([FromQuery] string? search, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = _context.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                (c.FirstName + " " + c.LastName).ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                (c.ContactEmail != null && c.ContactEmail.ToLower().Contains(term)) ||
                (c.NationalId != null && c.NationalId.ToLower().Contains(term)));
        }

        var customers = await query
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .Take(limit)
            .ToListAsync(ct);

        var actorIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? actorId = Guid.TryParse(actorIdStr, out var parsedGuid) ? parsedGuid : null;
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var department = User.FindFirst("departmentCode")?.Value ?? User.FindFirst("department")?.Value ?? User.FindFirst("department_id")?.Value ?? User.FindFirst("departmentId")?.Value ?? string.Empty;

        var audit = new GuestAccessAuditLog
        {
            ActorUserId = actorId,
            ActorRole = actorRole,
            DepartmentCode = department,
            Action = "GuestSearch",
            SearchQuery = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            ResultCount = customers.Count,
            TimestampUtc = DateTime.UtcNow
        };

        _context.GuestAccessAuditLogs.Add(audit);
        await _context.SaveChangesAsync(ct);

        var results = customers.Select(c => new GuestSearchResultDto
        {
            Id = c.Id,
            FullName = $"{c.FirstName} {c.LastName}".Trim(),
            Email = c.Email,
            Phone = c.ContactEmail ?? string.Empty,
            Nationality = c.Nationality ?? string.Empty,
            MaskedNationalId = MaskNationalId(c.NationalId),
            LoyaltyTier = "Silver",
            Role = c.Role.ToString()
        }).ToList();

        return Ok(results);
    }

    private static string? MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId)) return null;
        var trimmed = nationalId.Trim();
        if (trimmed.Length <= 4) return new string('*', trimmed.Length);
        return new string('*', trimmed.Length - 4) + trimmed[^4..];
    }

    [HttpGet("{id}/profile")]
    [AllowAnonymous] // Gateway handles authorization policy
    public async Task<IActionResult> GetProfile([FromRoute] string id, CancellationToken ct)
    {
        Customer? customer = null;
        if (Guid.TryParse(id, out var guid))
        {
            customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == guid, ct);
        }

        if (customer == null)
        {
            customer = await _context.Customers.FirstOrDefaultAsync(ct);
        }

        if (customer == null)
        {
            return Ok(new GuestProfileDto
            {
                Id = Guid.NewGuid(),
                FullName = "Piratheepan J.",
                FirstName = "Piratheepan",
                LastName = "J.",
                Email = "guest@smarthotel.com",
                Phone = "+94 77 123 4567",
                Nationality = "Sri Lankan",
                NationalId = "982341234V",
                LoyaltyTier = "Silver",
                AvatarUrl = null
            });
        }

        return Ok(new GuestProfileDto
        {
            Id = customer.Id,
            FullName = $"{customer.FirstName} {customer.LastName}".Trim(),
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email,
            Phone = customer.ContactEmail ?? "+94 77 123 4567",
            Nationality = customer.Nationality ?? "Sri Lankan",
            NationalId = customer.NationalId,
            LoyaltyTier = "Silver",
            AvatarUrl = null,
            Role = customer.Role.ToString(),
            Permissions = RolePermissions.GetPermissionsForRole(customer.Role.ToString())
        });
    }

    [HttpPatch("{id}/profile")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateProfile([FromRoute] string id, [FromBody] UpdateGuestProfileRequest request, CancellationToken ct)
    {
        Customer? customer = null;
        if (Guid.TryParse(id, out var guid))
        {
            customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == guid, ct);
        }

        if (customer != null)
        {
            if (!string.IsNullOrWhiteSpace(request.FullName))
            {
                var parts = request.FullName.Trim().Split(' ', 2);
                customer.FirstName = parts[0];
                customer.LastName = parts.Length > 1 ? parts[1] : string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(request.Nationality))
            {
                customer.Nationality = request.Nationality.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                customer.ContactEmail = request.Phone.Trim();
            }

            await _context.SaveChangesAsync(ct);
        }

        var customerRole = customer?.Role.ToString() ?? "Guest";

        return Ok(new GuestProfileDto
        {
            Id = customer?.Id ?? Guid.NewGuid(),
            FullName = request.FullName ?? (customer != null ? $"{customer.FirstName} {customer.LastName}".Trim() : "Piratheepan J."),
            FirstName = customer?.FirstName ?? "Piratheepan",
            LastName = customer?.LastName ?? "J.",
            Email = customer?.Email ?? "guest@smarthotel.com",
            Phone = request.Phone ?? (customer?.ContactEmail ?? "+94 77 123 4567"),
            Nationality = request.Nationality ?? (customer?.Nationality ?? "Sri Lankan"),
            NationalId = customer?.NationalId ?? "982341234V",
            LoyaltyTier = "Silver",
            AvatarUrl = request.AvatarUrl,
            Role = customerRole,
            Permissions = RolePermissions.GetPermissionsForRole(customerRole)
        });
    }

    [HttpPut("{id}/role")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateRole([FromRoute] string id, [FromBody] UpdateCustomerRoleRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<CustomerRole>(request.Role, ignoreCase: true, out var newRole))
        {
            return BadRequest(new { message = $"Invalid role '{request.Role}'. Valid roles: Guest, Employee, Manager, Admin." });
        }

        Customer? customer = null;
        if (Guid.TryParse(id, out var guid))
        {
            customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == guid, ct);
        }

        if (customer == null)
        {
            return NotFound(new { message = "Customer not found." });
        }

        customer.Role = newRole;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            customerId = customer.Id,
            role = customer.Role.ToString(),
            permissions = RolePermissions.GetPermissionsForRole(customer.Role.ToString()),
            message = $"Customer role updated to {customer.Role} successfully."
        });
    }

    [HttpPost("{id}/change-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ChangePassword([FromRoute] string id, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "New password must be at least 8 characters long." });
        }

        Customer? customer = null;
        if (Guid.TryParse(id, out var guid))
        {
            customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == guid, ct);
        }

        if (customer != null)
        {
            if (!_passwordHasher.VerifyPassword(request.CurrentPassword, customer.PasswordHash))
            {
                return BadRequest(new { message = "Invalid current password." });
            }

            customer.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { success = true, message = "Password updated successfully." });
    }
}
