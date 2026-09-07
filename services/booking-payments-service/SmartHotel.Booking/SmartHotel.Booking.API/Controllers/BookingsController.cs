using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Features.Bookings.Queries;

namespace SmartHotel.Booking.API.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BookingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Create a new reservation with concurrency conflict protection and capacity verification.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized(new { message = "User identity claim could not be determined." });
        }

        var result = await _mediator.Send(new CreateBookingCommand(request with { CustomerId = customerId.Value }), ct);
        if (!result.Succeeded)
        {
            if (result.Message.Contains("already booked", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("overlapping", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = result.Message });
            }
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(nameof(GetBookingById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Get all reservations (for staff dashboard or management).
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllBookings([FromQuery] int? limit, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAllBookingsQuery(limit), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Get single booking details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBookingById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBookingByIdQuery(id), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(result.Data);
    }

    /// <summary>
    /// Get list of bookings for the currently authenticated customer.
    /// </summary>
    [HttpGet("my")]
    [HttpGet("my-bookings")]
    [Authorize]
    [ProducesResponseType(typeof(List<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyBookings(CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized(new { message = "User identity claim could not be determined." });
        }

        var result = await _mediator.Send(new GetCustomerBookingsQuery(customerId.Value), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Get active stay for the currently authenticated customer (for Concierge and smart room features).
    /// </summary>
    [HttpGet("active-stay")]
    [Authorize]
    [ProducesResponseType(typeof(ActiveStayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveStay(CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized(new { message = "User identity claim could not be determined." });
        }

        var result = await _mediator.Send(new GetActiveStayQuery(customerId.Value), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message, isActive = false });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Cancel a booking and compute 3-tier refund policy.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(CancelBookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelBooking(Guid id, [FromBody] CancelBookingApiRequest? request, CancellationToken ct)
    {
        var isStaff = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Receptionist");
        var customerId = isStaff ? null : GetCurrentUserId();

        var result = await _mediator.Send(new CancelBookingCommand(id, customerId, request?.Reason), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Check in a confirmed reservation (Staff endpoint).
    /// </summary>
    [HttpPost("{id:guid}/check-in")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckIn(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CheckInCommand(id), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Check out a checked-in reservation (Staff endpoint). Publishes event to signal Hotel Ops room status to Dirty.
    /// </summary>
    [HttpPost("{id:guid}/check-out")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckOut(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CheckOutCommand(id), ct);
        if (!result.Succeeded)
        {
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

public record CancelBookingApiRequest(string? Reason = null);
