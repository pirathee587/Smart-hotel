using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Identity.Application.Interfaces;

namespace SmartHotel.Identity.API.Controllers;

[ApiController]
[AllowAnonymous]
[Produces("application/json")]
public class JwksController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;

    public JwksController(IJwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Returns the JSON Web Key Set (JWKS) containing public keys for token validation across microservices.
    /// </summary>
    [HttpGet(".well-known/jwks.json")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetJwks()
    {
        var jwks = _jwtTokenService.GetJwks();
        return Ok(jwks);
    }

    /// <summary>
    /// Returns OpenID Connect discovery metadata pointing to JWKS.
    /// </summary>
    [HttpGet(".well-known/openid-configuration")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetOpenIdConfiguration()
    {
        var scheme = Request.Scheme;
        var host = Request.Host.Value;
        var baseUrl = $"{scheme}://{host}";
        return Ok(new
        {
            issuer = "SmartHotel.Identity",
            jwks_uri = $"{baseUrl}/.well-known/jwks.json",
            response_types_supported = new[] { "token", "id_token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" }
        });
    }
}
