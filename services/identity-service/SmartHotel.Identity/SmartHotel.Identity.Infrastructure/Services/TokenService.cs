using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;

namespace SmartHotel.Identity.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAppDbContext _dbContext;
    private readonly int _accessTokenExpirationMinutes;
    private readonly int _refreshTokenExpirationDays;

    public TokenService(IJwtTokenService jwtTokenService, IAppDbContext dbContext, IConfiguration configuration)
    {
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
        _accessTokenExpirationMinutes = PositiveOrDefault(configuration["Jwt:AccessTokenExpirationMinutes"], 60);
        _refreshTokenExpirationDays = PositiveOrDefault(configuration["Jwt:RefreshTokenExpirationDays"], 7);
    }

    public async Task<IssuedTokenPair> IssueTokensAsync(Person person, CancellationToken ct = default)
    {
        var accessToken = _jwtTokenService.GenerateAccessToken(person, mustChangePassword: false);
        var refreshToken = GenerateRefreshToken();
        _dbContext.RefreshTokens.Add(CreateRecord(person.Id, refreshToken));
        await _dbContext.SaveChangesAsync(ct);
        return new IssuedTokenPair(accessToken, refreshToken, checked(_accessTokenExpirationMinutes * 60));
    }

    public async Task<IssuedTokenPair?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(refreshToken);
        var stored = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (stored == null || stored.RevokedAtUtc != null || stored.ExpiresAtUtc <= now || !CanAuthenticate(stored.User))
            return null;

        var replacement = GenerateRefreshToken();
        var replacementHash = HashToken(replacement);
        stored.RevokedAtUtc = now;
        stored.UpdatedAtUtc = now;
        stored.ReplacedByTokenHash = replacementHash;
        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = replacementHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_refreshTokenExpirationDays)
        });
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return new IssuedTokenPair(
            _jwtTokenService.GenerateAccessToken(stored.User, mustChangePassword: false),
            replacement,
            checked(_accessTokenExpirationMinutes * 60));
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;
        var tokenHash = HashToken(refreshToken);
        var stored = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        if (stored == null || stored.RevokedAtUtc != null) return false;
        stored.RevokedAtUtc = DateTime.UtcNow;
        stored.UpdatedAtUtc = stored.RevokedAtUtc;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
            token.UpdatedAtUtc = now;
        }
        if (activeTokens.Count > 0) await _dbContext.SaveChangesAsync(ct);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private RefreshToken CreateRecord(Guid userId, string rawToken)
    {
        var now = DateTime.UtcNow;
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_refreshTokenExpirationDays)
        };
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool CanAuthenticate(Person person) =>
        person.IsActive && (person is not Employee employee ||
            (employee.Status == SmartHotel.Identity.Domain.Enums.EmployeeStatus.Active && !employee.MustChangePassword));

    private static int PositiveOrDefault(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
