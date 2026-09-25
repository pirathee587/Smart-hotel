using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Authorization;
using SmartHotel.HotelOps.Application.Features.Images.Commands;
using SmartHotel.HotelOps.Application.Features.Images.Queries;
using SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;

namespace SmartHotel.HotelOps.API.Controllers;

[ApiController]
[Route("api/v1/room-types/{roomTypeId:guid}/images")]
[Produces("application/json")]
public class RoomTypeImagesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoomTypeImagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all images for a RoomType (Public access).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RoomTypeImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImages(Guid roomTypeId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRoomTypeImagesQuery(roomTypeId), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(result.Data);
    }

    /// <summary>
    /// Upload an image for a RoomType (Admin only).
    /// Enforces 5MB max file size and JPEG, PNG, WebP format validation.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RoomTypeImageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadImage(
        Guid roomTypeId,
        IFormFile file,
        [FromQuery] bool isPrimary = false,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file provided for upload." });
        }

        using var stream = file.OpenReadStream();
        var command = new UploadRoomTypeImageCommand(
            roomTypeId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            isPrimary);

        var result = await _mediator.Send(command, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Delete an image from a RoomType (Admin only).
    /// </summary>
    [HttpDelete("{imageId:guid}")]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(Guid roomTypeId, Guid imageId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteRoomTypeImageCommand(roomTypeId, imageId), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Reorder images for a RoomType (Admin only).
    /// </summary>
    [HttpPut("reorder")]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderImages(Guid roomTypeId, [FromBody] List<Guid> imageIds, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderRoomTypeImagesCommand(roomTypeId, imageIds), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }
        return Ok(new { message = result.Message });
    }
}
