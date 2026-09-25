using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Booking.Application.Features.Reviews.Commands;
using SmartHotel.Booking.Application.Features.Reviews.DTOs;
using SmartHotel.Booking.Application.Features.Reviews.Queries;
using SmartHotel.Authorization;

namespace SmartHotel.Booking.API.Controllers;

[ApiController]
[Route("api/v1/reviews")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Submit a review for a completed stay. Allowed only after checkout. Immutable once submitted.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        var result = await _mediator.Send(new CreateReviewCommand(request, customerId), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return Created($"/api/v1/reviews/{result.Data!.Id}", result.Data);
    }

    /// <summary>
    /// Get published reviews for a specific room type (Public endpoint).
    /// </summary>
    [HttpGet("room-type/{roomTypeId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewsByRoomType(Guid roomTypeId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetReviewsByRoomTypeQuery(roomTypeId), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// List all reviews for staff moderation.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(List<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllReviews([FromQuery] bool? isPublished, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAllReviewsQuery(isPublished), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Hide an inappropriate review from public display (Admin/Manager only).
    /// </summary>
    [HttpPost("{id:guid}/hide")]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HideReview(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new HideReviewCommand(id), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Unhide a previously hidden review (Admin/Manager only).
    /// </summary>
    [HttpPost("{id:guid}/unhide")]
    [Authorize(Policy = HotelPolicies.FrontOfficeManagement)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnhideReview(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new UnhideReviewCommand(id), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
