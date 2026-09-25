using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Booking.Application.Features.Complaints.Commands;
using SmartHotel.Booking.Application.Features.Complaints.DTOs;
using SmartHotel.Booking.Application.Features.Complaints.Queries;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Authorization;

namespace SmartHotel.Booking.API.Controllers;

[ApiController]
[Route("api/v1/complaints")]
[Produces("application/json")]
public class ComplaintsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _authorization;

    public ComplaintsController(IMediator mediator, IAuthorizationService authorization)
    {
        _mediator = mediator;
        _authorization = authorization;
    }

    /// <summary>
    /// File a new complaint with SLA calculation based on severity.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ComplaintDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FileComplaint([FromBody] FileComplaintRequest request, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized(new { message = "User identity claim could not be determined." });
        }

        var result = await _mediator.Send(new FileComplaintCommand(request, customerId.Value), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(nameof(GetComplaintById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Get list of complaints submitted by the authenticated customer.
    /// </summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(List<ComplaintDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyComplaints(CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized(new { message = "User identity claim could not be determined." });
        }

        var result = await _mediator.Send(new GetCustomerComplaintsQuery(customerId.Value), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// List complaints with optional status/severity filters or overdue SLA filter (Staff/Admin).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    [ProducesResponseType(typeof(List<ComplaintDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComplaints(
        [FromQuery] ComplaintStatus? status,
        [FromQuery] ComplaintSeverity? severity,
        [FromQuery] bool overdueOnly = false,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetComplaintsQuery(status, severity, overdueOnly), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Get single complaint details including its timeline audit log.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ComplaintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComplaintById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetComplaintByIdQuery(id), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }

        var isStaff = (await _authorization.AuthorizeAsync(User, HotelPolicies.FrontOfficeOperations)).Succeeded;
        var customerId = GetCurrentUserId();

        if (!isStaff && (!customerId.HasValue || result.Data!.CustomerId != customerId.Value))
        {
            return Forbid();
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Update complaint status, resolve with notes, escalate, or close (Staff/Admin).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    [ProducesResponseType(typeof(ComplaintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateComplaintStatus(
        Guid id,
        [FromBody] UpdateComplaintStatusRequest request,
        CancellationToken ct)
    {
        var changedBy = User.Identity?.Name ?? User.FindFirst("name")?.Value ?? "Staff";
        var result = await _mediator.Send(new UpdateComplaintStatusCommand(id, request, changedBy), ct);
        if (!result.Succeeded)
        {
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = result.Message });
            }
            return BadRequest(new { message = result.Message });
        }

        return Ok(result.Data);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
