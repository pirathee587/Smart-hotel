using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.HotelOps.Application.Features.Departments.Commands;
using SmartHotel.HotelOps.Application.Features.Departments.DTOs;
using SmartHotel.HotelOps.Application.Features.Departments.Queries;

namespace SmartHotel.HotelOps.API.Controllers;

[ApiController]
[Route("api/v1/departments")]
[Produces("application/json")]
public class DepartmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DepartmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all departments (optionally filtered by hotelId).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartments([FromQuery] Guid? hotelId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDepartmentsQuery(hotelId), ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Create a new department (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateDepartmentCommand(request), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }
        return CreatedAtAction(nameof(GetDepartments), new { hotelId = result.Data!.HotelId }, result.Data);
    }
}
