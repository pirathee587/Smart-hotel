using SmartHotel.Identity.Domain.Entities;

namespace SmartHotel.Identity.Application.Interfaces;

public interface ITokenService
{
    Task<IssuedTokenPair> IssueTokensAsync(Person person, CancellationToken ct = default);
    Task<IssuedTokenPair?> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> RevokeAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAllAsync(Guid userId, CancellationToken ct = default);
    string GenerateRefreshToken();
}

public sealed record IssuedTokenPair(string AccessToken, string RefreshToken, int ExpiresInSeconds);
