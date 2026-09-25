using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Identity.Application.Features.Auth.Commands;

namespace SmartHotel.Identity.API.Controllers;

[ApiController]
[Produces("application/json")]
public class IdentityController : ControllerBase
{
    private readonly ILoginCommandHandler _loginCommandHandler;

    public IdentityController(ILoginCommandHandler loginCommandHandler)
    {
        _loginCommandHandler = loginCommandHandler;
    }

    /// <summary>
    /// Authenticate a SmartHotel member and receive JWT access token and refresh token.
    /// </summary>
    [HttpPost("api/identity/login")]
    [HttpPost("api/v1/identity/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MemberLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await _loginCommandHandler.HandleAsync(command, ct);
        if (!result.Succeeded || result.Data == null)
        {
            // Never leak whether it was the username or password that was wrong
            return Unauthorized(new { error = "invalid_credentials" });
        }

        return Ok(result.Data);
    }
}
