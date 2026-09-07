using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Identity.Application.Features.Auth.Models;
using SmartHotel.Identity.Application.Features.Auth.Services;

namespace SmartHotel.Identity.API.Controllers;

[ApiController]
[Route("api/v1/portal-auth")]
[Produces("application/json")]
public class PortalAuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public PortalAuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Request a passwordless magic login link for a customer account (rate-limited to 5/hour).
    /// Returns an identical response whether the email exists or not to prevent enumeration.
    /// </summary>
    [HttpPost("magic-link")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RequestMagicLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestMagicLink([FromBody] RequestMagicLinkCommand command, CancellationToken ct)
    {
        var result = await _authService.RequestMagicLinkAsync(command, ct);
        if (!result.Succeeded)
        {
            if (result.Message.Contains("Too many", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = result.Message });
            }
            return BadRequest(new { message = result.Message, errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Verify a single-use magic link token and receive an RS256 JWT access token.
    /// Fails with EMAIL_NOT_VERIFIED if customer email has not yet been verified.
    /// </summary>
    [HttpPost("verify-magic-link")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyMagicLink([FromBody] VerifyMagicLinkCommand command, CancellationToken ct)
    {
        var result = await _authService.VerifyMagicLinkAsync(command, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = result.Message, errors = result.Errors });
        }

        return Ok(result.Data);
    }
}
