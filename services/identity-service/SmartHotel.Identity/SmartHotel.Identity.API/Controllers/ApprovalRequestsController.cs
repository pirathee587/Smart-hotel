using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Infrastructure.Persistence;

namespace SmartHotel.Identity.API.Controllers;

public sealed record CreateApprovalRequest(string Type, string Description);

[ApiController]
[Route("api/v1/approval-requests")]
[Authorize]
public class ApprovalRequestsController : ControllerBase
{
    private readonly AppDbContext _context;
    public ApprovalRequestsController(AppDbContext context) => _context = context;

    [HttpGet]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<IReadOnlyList<ApprovalRequest>>> GetAll(CancellationToken ct) =>
        Ok(await _context.ApprovalRequests.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ApprovalRequest>> Create(CreateApprovalRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { message = "Type and Description are required." });

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        Guid? userId = Guid.TryParse(userIdStr, out var parsedGuid) ? parsedGuid : null;

        var approval = new ApprovalRequest
        {
            Type = request.Type.Trim(),
            Description = request.Description.Trim(),
            RequestedBy = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "Unknown",
            RequestedByUserId = userId
        };
        _context.ApprovalRequests.Add(approval);
        await _context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAll), new { id = approval.Id }, approval);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Owner")]
    public Task<ActionResult<ApprovalRequest>> Approve(Guid id, CancellationToken ct) => SetStatus(id, "Active", ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Owner")]
    public Task<ActionResult<ApprovalRequest>> Reject(Guid id, CancellationToken ct) => SetStatus(id, "Rejected", ct);

    private async Task<ActionResult<ApprovalRequest>> SetStatus(Guid id, string status, CancellationToken ct)
    {
        var approval = await _context.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (approval is null) return NotFound(new { message = "Approval request not found." });
        if (approval.Status != "PendingApproval") return BadRequest(new { message = "Approval request has already been reviewed." });

        // Separation of duties: Never allow requester to self-approve or self-review their own request
        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        Guid? currentUserId = Guid.TryParse(currentUserIdStr, out var parsedId) ? parsedId : null;
        var currentEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;

        bool isSelfApproval = (approval.RequestedByUserId.HasValue && currentUserId.HasValue && approval.RequestedByUserId.Value == currentUserId.Value)
            || (!string.IsNullOrWhiteSpace(currentEmail) && string.Equals(approval.RequestedBy, currentEmail, StringComparison.OrdinalIgnoreCase));

        if (isSelfApproval)
        {
            return Conflict(new { message = "Separation of duties violation: A requester cannot approve or review their own approval request." });
        }

        approval.Status = status;
        approval.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Ok(approval);
    }
}
