using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Infrastructure.Persistence;
using SmartHotel.Identity.Infrastructure.Services;

namespace SmartHotel.Identity.UnitTests;

public class RefreshTokenServiceTests
{
    [Fact]
    public async Task IssueTokens_PersistsHashWithSevenDayExpiry()
    {
        var (db, service, customer) = CreateContext();
        var before = DateTime.UtcNow;

        var issued = await service.IssueTokensAsync(customer);
        var stored = await db.RefreshTokens.SingleAsync();

        issued.ExpiresInSeconds.Should().Be(3600);
        issued.RefreshToken.Should().NotBeNullOrWhiteSpace();
        stored.TokenHash.Should().NotBe(issued.RefreshToken);
        stored.TokenHash.Should().HaveLength(64);
        stored.ExpiresAtUtc.Should().BeCloseTo(before.AddDays(7), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ValidRefresh_RotatesToken_AndOldTokenCannotBeReused()
    {
        var (db, service, customer) = CreateContext();
        var original = await service.IssueTokensAsync(customer);

        var rotated = await service.RefreshAsync(original.RefreshToken);
        var reuse = await service.RefreshAsync(original.RefreshToken);

        rotated.Should().NotBeNull();
        rotated!.RefreshToken.Should().NotBe(original.RefreshToken);
        reuse.Should().BeNull();
        (await db.RefreshTokens.CountAsync()).Should().Be(2);
        (await db.RefreshTokens.OrderBy(t => t.CreatedAtUtc).FirstAsync()).RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ExpiredRevokedUnknownAndInactiveTokens_AreRejected()
    {
        var (db, service, customer) = CreateContext();
        var expired = await service.IssueTokensAsync(customer);
        (await db.RefreshTokens.SingleAsync()).ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        (await service.RefreshAsync(expired.RefreshToken)).Should().BeNull();

        db.RefreshTokens.RemoveRange(db.RefreshTokens);
        await db.SaveChangesAsync();
        var revoked = await service.IssueTokensAsync(customer);
        (await service.RevokeAsync(revoked.RefreshToken)).Should().BeTrue();
        (await service.RefreshAsync(revoked.RefreshToken)).Should().BeNull();
        (await service.RefreshAsync("unknown-token")).Should().BeNull();

        var active = await service.IssueTokensAsync(customer);
        customer.IsActive = false;
        await db.SaveChangesAsync();
        (await service.RefreshAsync(active.RefreshToken)).Should().BeNull();
    }

    [Fact]
    public async Task RevokeAll_RevokesOnlyTheSpecifiedUsersTokens()
    {
        var (db, service, customer) = CreateContext();
        var other = new Customer { Email = "other@test.local", FirstName = "Other", LastName = "User", IsActive = true };
        db.Customers.Add(other);
        await db.SaveChangesAsync();
        var first = await service.IssueTokensAsync(customer);
        var second = await service.IssueTokensAsync(other);

        await service.RevokeAllAsync(customer.Id);

        (await service.RefreshAsync(first.RefreshToken)).Should().BeNull();
        (await service.RefreshAsync(second.RefreshToken)).Should().NotBeNull();
    }

    private static (AppDbContext Db, TokenService Service, Customer Customer) CreateContext()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var customer = new Customer
        {
            Email = "customer@test.local",
            FirstName = "Test",
            LastName = "Customer",
            IsActive = true,
            EmailVerified = true
        };
        db.Customers.Add(customer);
        db.SaveChanges();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:AccessTokenExpirationMinutes"] = "60",
            ["Jwt:RefreshTokenExpirationDays"] = "7"
        }).Build();
        return (db, new TokenService(new StubJwtTokenService(), db, configuration), customer);
    }

    private sealed class StubJwtTokenService : IJwtTokenService
    {
        public string GenerateAccessToken(Person person, bool mustChangePassword = false) => $"access-{person.Id}";
        public object GetJwks() => new { keys = Array.Empty<object>() };
    }
}
