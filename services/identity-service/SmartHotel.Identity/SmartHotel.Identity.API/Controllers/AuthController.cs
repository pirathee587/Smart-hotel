using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Identity.Application.Features.Auth.Models;
using SmartHotel.Identity.Application.Features.Auth.Services;
using SmartHotel.Identity.Application.Interfaces;

namespace SmartHotel.Identity.API.Controllers;

[ApiController]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;

    public AuthController(IAuthService authService, ITokenService tokenService)
    {
        _authService = authService;
        _tokenService = tokenService;
    }

    [HttpPost("api/v1/auth/refresh")]
    [HttpPost("api/identity/refresh")]
    [HttpPost("api/v1/identity/refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var tokens = await _tokenService.RefreshAsync(request.RefreshToken, ct);
        if (tokens == null)
        {
            return Unauthorized(new { message = "Invalid refresh token." });
        }

        return Ok(new
        {
            accessToken = tokens.AccessToken,
            token = tokens.AccessToken,
            refreshToken = tokens.RefreshToken,
            tokenType = "Bearer",
            expiresIn = tokens.ExpiresInSeconds
        });
    }

    [HttpPost("api/v1/auth/logout")]
    [HttpPost("api/identity/logout")]
    [HttpPost("api/v1/identity/logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _tokenService.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>
    /// Register a new customer account.
    /// </summary>
    [HttpPost("api/v1/customers/register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CustomerRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterCustomerAsync(request, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message, errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Authenticate a customer and receive an RS256 JWT access token.
    /// </summary>
    [HttpPost("api/v1/customers/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CustomerLogin([FromBody] CustomerLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.CustomerLoginAsync(request, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = result.Message, errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Authenticate a user with Google OAuth ID token.
    /// </summary>
    [HttpPost("api/auth/google")]
    [HttpPost("api/v1/auth/google")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.GoogleLoginAsync(request, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = result.Message, errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Authenticate or register a customer using Google OAuth ID Token.
    /// </summary>
    [HttpPost("api/v1/customers/google-login")]
    [HttpPost("api/customers/google-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CustomerGoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.GoogleLoginAsync(request, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = result.Message, errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Authenticate an employee and receive an RS256 JWT access token.
    /// </summary>
    [HttpPost("api/v1/auth/employee/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EmployeeLogin([FromBody] EmployeeLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.EmployeeLoginAsync(request, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = result.Message, errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Verify a user's email address using a verification token.
    /// </summary>
    [HttpGet("api/v1/auth/verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string email, [FromQuery] string token, CancellationToken ct)
    {
        var result = await _authService.VerifyEmailAsync(email, token, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message, errors = result.Errors });
        }

        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Resend an email verification link (rate limited to 5 attempts/hour).
    /// </summary>
    [HttpPost("api/v1/auth/resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        var result = await _authService.ResendVerificationEmailAsync(request.Email, ct);
        if (!result.Succeeded)
        {
            if (result.Message.Contains("Too many", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = result.Message });
            }
            return BadRequest(new { message = result.Message, errors = result.Errors });
        }

        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Request a password reset link (returns identical response whether email exists or not).
    /// </summary>
    [HttpPost("api/v1/auth/forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ForgotPasswordAsync(request.Email, ct);
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
    /// Reset password using a valid reset token.
    /// </summary>
    [HttpPost("api/v1/auth/reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ResetPasswordAsync(request, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message, errors = result.Errors });
        }

        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Change password for currently authenticated user.
    /// </summary>
    [HttpPost("api/v1/auth/change-password")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid token subject claim." });
        }

        var mustChangePasswordClaim = User.FindFirst("must_change_password")?.Value;
        var isMustChangePasswordScope = string.Equals(mustChangePasswordClaim, "true", StringComparison.OrdinalIgnoreCase);

        var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, isMustChangePasswordScope, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message, errors = result.Errors });
        }

        return Ok(new
        {
            message = result.Message,
            accessToken = result.Data?.AccessToken,
            token = result.Data?.Token,
            refreshToken = result.Data?.RefreshToken,
            tokenType = result.Data?.TokenType,
            expiresIn = result.Data?.ExpiresIn,
            mustChangePassword = false,
            user = result.Data?.User
        });
    }

    /// <summary>
    /// Get currently authenticated user profile.
    /// </summary>
    [HttpGet("api/v1/auth/me")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetCurrentUser()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var dept = User.FindFirst("departmentId")?.Value;

        return Ok(new
        {
            id = sub,
            email,
            role,
            departmentId = dept
        });
    }
}
