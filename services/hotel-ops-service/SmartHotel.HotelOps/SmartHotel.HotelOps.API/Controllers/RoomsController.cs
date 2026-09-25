using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SmartHotel.HotelOps.Application.Features.Rooms.Commands;
using SmartHotel.HotelOps.Application.Features.Rooms.DTOs;
using SmartHotel.HotelOps.Application.Features.Rooms.Queries;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.Authorization;

namespace SmartHotel.HotelOps.API.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Produces("application/json")]
public class RoomsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _authorization;
    private readonly IConfiguration _configuration;

    public RoomsController(IMediator mediator, IAuthorizationService authorization, IConfiguration configuration)
    {
        _mediator = mediator;
        _authorization = authorization;
        _configuration = configuration;
    }

    /// <summary>
    /// Check availability for all published room types (or a specific one) for the given date range.
    /// Public endpoint — no authentication required.
    /// </summary>
    [HttpGet("availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RoomAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        [FromQuery] Guid? roomTypeId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAvailabilityQuery(checkIn, checkOut, roomTypeId), ct);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Message });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get flat list of all rooms.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<RoomDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRooms([FromQuery] Guid? hotelId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoomsQuery(hotelId), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Get floor-view grouping of rooms (Floor -> Category -> Room badges).
    /// </summary>
    [HttpGet("floor-view")]
    [Authorize]
    [ProducesResponseType(typeof(List<FloorViewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFloorView([FromQuery] Guid? hotelId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoomsGroupedByFloorQuery(hotelId), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Get single room by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoomByIdQuery(id), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(result.Data);
    }

    /// <summary>
    /// Internal authenticated room lookup for cross-service calls (e.g. Booking Service).
    /// Requires valid X-Service-Api-Key.
    /// </summary>
    [HttpGet("internal/{id:guid}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInternalRoomById(Guid id, CancellationToken ct)
    {
        if (!ServiceAuthorized())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Service authorization required." });
        }

        var result = await _mediator.Send(new GetRoomByIdQuery(id), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(result.Data);
    }

    private bool ServiceAuthorized()
    {
        var expected = _configuration["HOTEL_OPS_SERVICE_API_KEY"] ?? _configuration["Services:ServiceApiKey"];
        var supplied = Request.Headers["X-Service-Api-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }

    /// <summary>
    /// Create a new room (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateRoomCommand(request), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }
        return CreatedAtAction(nameof(GetRoomById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Transition room status through the domain state machine.
    /// Writes transactional Outbox message to publish room.status_changed event.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoomStatus(
        Guid id,
        [FromBody] UpdateRoomStatusRequest request,
        CancellationToken ct)
    {
        if (request.NewStatus is RoomStatus.InCleaning or RoomStatus.Inspected or RoomStatus.Available or RoomStatus.OutOfOrder)
        {
            return Conflict(new { message = "Task-linked Housekeeping or Maintenance readiness APIs are required for this room transition." });
        }
        var policy = request.NewStatus switch
        {
            RoomStatus.Occupied or RoomStatus.Dirty => HotelPolicies.FrontOfficeOperations,
            _ => null
        };
        if (policy is null || !(await _authorization.AuthorizeAsync(User, policy)).Succeeded)
        {
            return Forbid();
        }

        var command = new UpdateRoomStatusCommand(id, request.NewStatus, request.Reason);
        var result = await _mediator.Send(command, ct);

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

    /// <summary>
    /// Get candidate upgrade rooms with price delta vs. currently held rate.
    /// </summary>
    [HttpGet("{roomId}/upsell-options")]
    [HttpGet("/api/rooms/{roomId}/upsell-options")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<UpsellOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpsellOptions(
        [FromRoute] string roomId,
        [FromQuery] DateOnly? checkIn,
        [FromQuery] DateOnly? checkOut,
        [FromQuery] int? guests,
        [FromQuery] decimal? currentPrice,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUpsellOptionsQuery(roomId, checkIn, checkOut, guests, currentPrice), ct);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Message });

        return Ok(result.Data);
    }
}
