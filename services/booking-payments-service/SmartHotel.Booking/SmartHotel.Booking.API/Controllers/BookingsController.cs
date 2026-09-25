using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Features.Bookings.Queries;
using SmartHotel.Authorization;

namespace SmartHotel.Booking.API.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _authorization;

    public BookingsController(IMediator mediator, IAuthorizationService authorization)
    {
        _mediator = mediator;
        _authorization = authorization;
    }

    /// <summary>
    /// Check room availability for the given room type and date range.
    /// Called by hotel-ops-service to surface availability on the public listing page.
    /// </summary>
    [HttpGet("availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomAvailabilityResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] Guid roomTypeId,
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CheckRoomAvailabilityQuery(roomTypeId, checkIn, checkOut), ct);

        if (!result.Succeeded)
            return BadRequest(new { message = result.Message });

        return Ok(result.Data);
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
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    [ProducesResponseType(typeof(List<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllBookings(
        [FromQuery] int? limit,
        [FromQuery] SmartHotel.Booking.Domain.Enums.BookingStatus? status,
        [FromQuery] string? search,
        [FromQuery] DateOnly? checkInDate,
        [FromQuery] DateOnly? checkOutDate,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAllBookingsQuery(limit, status, search, checkInDate, checkOutDate), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Front Office operational summary (today arrivals, departures, pending check-ins/outs).
    /// </summary>
    [HttpGet("frontoffice/summary")]
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    [ProducesResponseType(typeof(FrontOfficeSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFrontOfficeSummary(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFrontOfficeSummaryQuery(), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Get audit history for a reservation.
    /// </summary>
    [HttpGet("{id:guid}/audit-logs")]
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    [ProducesResponseType(typeof(List<FrontOfficeAuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBookingAuditLogsQuery(id), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Assign or reassign a room to a reservation (Front Office staff endpoint).
    /// </summary>
    [HttpPost("{id:guid}/assign-room")]
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignRoom(Guid id, [FromBody] AssignRoomApiRequest request, CancellationToken ct)
    {
        var actor = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var actorId) ? actorId : (Guid?)null;
        var command = new AssignRoomCommand(id, request.NewRoomId, actor, request.Reason);
        var result = await _mediator.Send(command, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }
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
        if (result.Data!.CustomerId != GetCurrentUserId()
            && !(await _authorization.AuthorizeAsync(User, HotelPolicies.FrontOfficeOperations)).Succeeded)
        {
            return Forbid();
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
        var isStaff = (await _authorization.AuthorizeAsync(User, HotelPolicies.FrontOfficeOperations)).Succeeded;
        if(isStaff&&(!(await _authorization.AuthorizeAsync(User,HotelPolicies.FrontOfficeManagement)).Succeeded||string.IsNullOrWhiteSpace(request?.Reason)))return StatusCode(StatusCodes.Status403Forbidden,new{message="Front Office Manager authorization and an override reason are required for staff cancellation."});
        var customerId = isStaff ? null : GetCurrentUserId();
        if (!isStaff && !customerId.HasValue)
        {
            return Unauthorized(new { message = "User identity claim could not be determined." });
        }

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
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckIn(Guid id, CancellationToken ct)
    {
        var actor=Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??User.FindFirst("sub")?.Value,out var actorId)?actorId:(Guid?)null;
        var result = await _mediator.Send(new CheckInCommand(id,actor,"FrontOffice"), ct);
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
    [Authorize(Policy = HotelPolicies.FrontOfficeOperations)]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckOut(Guid id, CancellationToken ct)
    {
        var actor = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var actorId) ? actorId : (Guid?)null;
        var result = await _mediator.Send(new CheckOutCommand(id, actor), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Initialize a new booking draft.
    /// </summary>
    [HttpPost("draft")]
    [HttpPost("/api/bookings/draft")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BookingDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateDraftCommand command, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        var customerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
        var result = await _mediator.Send(command with
        {
            CustomerId = customerId ?? command.CustomerId,
            CustomerEmail = !string.IsNullOrWhiteSpace(customerEmail) ? customerEmail : command.CustomerEmail
        }, ct);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Message });

        return Ok(result.Data);
    }

    /// <summary>
    /// Update an in-progress booking draft with an upgraded room/rate, recalculating price server-authoritatively.
    /// </summary>
    [HttpPost("draft/{draftId:guid}/upgrade")]
    [HttpPost("/api/bookings/draft/{draftId:guid}/upgrade")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BookingDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApplyUpgrade(
        [FromRoute] Guid draftId,
        [FromBody] ApplyUpgradeRequest request,
        CancellationToken ct)
    {
        var command = new ApplyUpgradeCommand(
            draftId,
            request.NewRoomId,
            request.NewRatePlanId,
            request.NewRoomName,
            request.NewRatePlanName);

        var result = await _mediator.Send(command, ct);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Message });

        return Ok(result.Data);
    }

    /// <summary>
    /// Finalize checkout, process tokenized payment, and generate booking reservation reference.
    /// </summary>
    [HttpPost("checkout")]
    [HttpPost("/api/bookings/checkout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CheckoutResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        var command = new CheckoutCommand(request, customerId);
        var result = await _mediator.Send(command, ct);

        if (!result.Succeeded)
        {
            if (result.Message.Contains("already booked", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = result.Message });
            }
            if (result.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = result.Message });
            }
            if (result.Message.Contains("Authentication required", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { message = result.Message });
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

public record CancelBookingApiRequest(string? Reason = null);
public record AssignRoomApiRequest(Guid NewRoomId, string? Reason = null);
