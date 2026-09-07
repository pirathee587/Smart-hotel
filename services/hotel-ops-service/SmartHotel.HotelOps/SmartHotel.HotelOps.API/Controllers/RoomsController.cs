using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.HotelOps.Application.Features.Rooms.Commands;
using SmartHotel.HotelOps.Application.Features.Rooms.DTOs;
using SmartHotel.HotelOps.Application.Features.Rooms.Queries;
using SmartHotel.HotelOps.Domain.Enums;

namespace SmartHotel.HotelOps.API.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Produces("application/json")]
public class RoomsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoomsController(IMediator mediator)
    {
        _mediator = mediator;
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
    /// Create a new room (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
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
}
