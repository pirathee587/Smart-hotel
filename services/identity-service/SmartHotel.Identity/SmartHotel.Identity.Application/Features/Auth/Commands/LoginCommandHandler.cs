using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.Application.Features.Auth.Commands;

public interface ILoginCommandHandler
{
    Task<LoginCommandResult> HandleAsync(LoginCommand command, CancellationToken ct = default);
}

public class LoginCommandHandler : ILoginCommandHandler
{
    private readonly IAppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRateLimiterService _rateLimiter;
    private readonly ILogger<LoginCommandHandler> _logger;

    // Rate limiting: 5 failed attempts per 15 minutes per username / IP
    private const int MaxLoginAttempts = 10;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(15);

    public LoginCommandHandler(
        IAppDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRateLimiterService rateLimiter,
        ILogger<LoginCommandHandler> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<LoginCommandResult> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        var username = command.Username?.Trim() ?? string.Empty;
        var password = command.Password ?? string.Empty;

        // 1. Validate empty inputs
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Member login rejected due to empty credentials.");
            return LoginCommandResult.InvalidCredentials();
        }

        // 2. Fixed-window rate limiting to prevent credential stuffing
        var rateLimitKey = $"login:member:{username.ToLowerInvariant()}";
        if (!_rateLimiter.CheckAndRecordAttempt(rateLimitKey, MaxLoginAttempts, RateLimitWindow))
        {
            _logger.LogWarning("Rate limit exceeded for member login attempts on username: {Username}", username);
            return LoginCommandResult.InvalidCredentials();
        }

        // 3. Find Customer/Person by email (or username)
        var normalizedUser = username.ToLowerInvariant();
        var person = await _dbContext.Persons
            .FirstOrDefaultAsync(p => p.Email.ToLower() == normalizedUser, ct);

        if (person == null)
        {
            // Log failed attempt without leaking password
            _logger.LogWarning("Failed login attempt: User not found for username: {Username}", username);
            return LoginCommandResult.InvalidCredentials();
        }

        // 4. Verify password hash
        if (!_passwordHasher.VerifyPassword(password, person.PasswordHash))
        {
            // Log failed attempt without logging password
            _logger.LogWarning("Failed login attempt: Incorrect password for username: {Username}", username);
            return LoginCommandResult.InvalidCredentials();
        }

        // 5. Check active status
        if (!person.IsActive)
        {
            _logger.LogWarning("Failed login attempt: Account deactivated for username: {Username}", username);
            return LoginCommandResult.InvalidCredentials();
        }

        if (person is Employee employee && employee.Status != EmployeeStatus.Active)
        {
            _logger.LogWarning("Member login rejected for unapproved employee ID: {EmployeeId}, status: {Status}", employee.Id, employee.Status);
            return LoginCommandResult.InvalidCredentials();
        }

        // 6. Issue tokens with consistent claims
        var issuedTokens = await _tokenService.IssueTokensAsync(person, ct);

        var displayName = $"{person.FirstName} {person.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = person.Email;
        }

        _logger.LogInformation("Member login successful for user ID: {UserId}", person.Id);

        return LoginCommandResult.Success(new MemberLoginResponse
        {
            AccessToken = issuedTokens.AccessToken,
            RefreshToken = issuedTokens.RefreshToken,
            ExpiresIn = issuedTokens.ExpiresInSeconds,
            Member = new MemberDto
            {
                Id = person.Id,
                DisplayName = displayName
            }
        });
    }
}
