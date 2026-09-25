using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.HotelOps.Application.Features.RoomTypes.Commands;
using SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;
using SmartHotel.HotelOps.Application.Features.RoomTypes.Queries;
using SmartHotel.Authorization;

namespace SmartHotel.HotelOps.API.Controllers;

[ApiController]
[Route("api/v1/room-types")]
[Produces("application/json")]
public class RoomTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoomTypesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get list of room types. Non-staff callers see only published room types.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RoomTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoomTypes(CancellationToken ct)
    {
        var isStaff = User.Identity?.IsAuthenticated == true &&
                      (User.IsInRole("Admin") ||
                       User.IsInRole("Manager") ||
                       User.IsInRole("Receptionist") ||
                       User.IsInRole("Housekeeper"));

        var result = await _mediator.Send(new GetRoomTypesQuery(isStaff), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Get single room type details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomTypeById(Guid id, CancellationToken ct)
    {
        var isStaff = User.Identity?.IsAuthenticated == true &&
                      (User.IsInRole("Admin") ||
                       User.IsInRole("Manager") ||
                       User.IsInRole("Receptionist"));

        var result = await _mediator.Send(new GetRoomTypeByIdQuery(id, isStaff), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(result.Data);
    }

    /// <summary>
    /// Create a new draft room type (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(RoomTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRoomType([FromBody] CreateRoomTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateRoomTypeCommand(request), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }
        return CreatedAtAction(nameof(GetRoomTypeById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Update existing room type (Admin only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(RoomTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoomType(Guid id, [FromBody] UpdateRoomTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateRoomTypeCommand(id, request), ct);
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
    /// Publish draft room type to make it visible to customers (Admin only).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishRoomType(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new PublishRoomTypeCommand(id), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(new { message = result.Message });
    }
}
