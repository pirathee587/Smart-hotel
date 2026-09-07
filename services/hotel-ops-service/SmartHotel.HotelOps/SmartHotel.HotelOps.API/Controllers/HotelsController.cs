using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.HotelOps.Application.Features.Hotels.Commands;
using SmartHotel.HotelOps.Application.Features.Hotels.DTOs;
using SmartHotel.HotelOps.Application.Features.Hotels.Queries;

namespace SmartHotel.HotelOps.API.Controllers;

[ApiController]
[Route("api/v1/hotels")]
[Produces("application/json")]
public class HotelsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HotelsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all hotels.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<HotelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHotels(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetHotelsQuery(), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Update hotel details (Admin only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(HotelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateHotel(Guid id, [FromBody] UpdateHotelRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateHotelCommand(id, request), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(result.Data);
    }
}
