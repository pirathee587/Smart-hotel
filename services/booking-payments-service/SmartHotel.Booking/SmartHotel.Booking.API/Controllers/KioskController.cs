using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Booking.Application.Features.Kiosk.Commands;
using SmartHotel.Booking.Application.Features.Kiosk.DTOs;
using SmartHotel.Booking.Application.Features.Kiosk.Queries;

namespace SmartHotel.Booking.API.Controllers;

[ApiController]
[Route("api/v1/kiosk")]
[AllowAnonymous]
[Produces("application/json")]
public class KioskController : ControllerBase
{
    private readonly IMediator _mediator;

    public KioskController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Kiosk reservation lookup by Booking Reference and Customer Last Name.
    /// </summary>
    [HttpPost("lookup")]
    [ProducesResponseType(typeof(KioskBookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupReservation([FromBody] KioskLookupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new KioskLookupBookingQuery(request.BookingReference, request.LastName), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Kiosk self-service check-in.
    /// </summary>
    [HttpPost("check-in")]
    [ProducesResponseType(typeof(KioskBookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SelfCheckIn([FromBody] KioskActionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new KioskCheckInCommand(request.BookingReference, request.LastName), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Kiosk self-service check-out. Triggers room dirty event for housekeeping.
    /// </summary>
    [HttpPost("check-out")]
    [ProducesResponseType(typeof(KioskBookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SelfCheckOut([FromBody] KioskActionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new KioskCheckOutCommand(request.BookingReference, request.LastName), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result.Data);
    }
}

public record KioskLookupRequest(string BookingReference, string LastName);
public record KioskActionRequest(string BookingReference, string LastName);
