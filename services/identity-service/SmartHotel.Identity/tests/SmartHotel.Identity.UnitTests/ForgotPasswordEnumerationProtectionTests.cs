using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.Identity.Application.Features.Auth.Services;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Infrastructure.Persistence;
using SmartHotel.Identity.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Identity.UnitTests;

public class ForgotPasswordEnumerationProtectionTests
{
    [Fact]
    public async Task ForgotPasswordAsync_ReturnsIdenticalResponse_ForExistingAndNonExistingEmail()
    {
        // 1. Setup in-memory DbContext
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new AppDbContext(options);
        var passwordHasher = new PasswordHasher();
        var fakeEmailService = new FakeEmailService(NullLogger<FakeEmailService>.Instance);
        var rateLimiter = new InMemoryRateLimiterService();

        // Seed an existing customer
        var existingEmail = "registered.customer@smarthotel.com";
        var existingCustomer = new Customer
        {
            FirstName = "Alice",
            LastName = "Walker",
            Email = existingEmail,
            PasswordHash = passwordHasher.HashPassword("StrongP@ssw0rd2026!"),
            EmailVerified = true,
            IsActive = true
        };
        dbContext.Customers.Add(existingCustomer);
        await dbContext.SaveChangesAsync();

        var authService = new AuthService(
            dbContext,
            passwordHasher,
            null!, // jwtTokenService not needed for forgot-password
            fakeEmailService,
            rateLimiter);

        // 2. Call with existing email
        var existingResult = await authService.ForgotPasswordAsync(existingEmail);

        // 3. Call with non-existing email
        var nonExistingEmail = "unregistered.stranger@smarthotel.com";
        var nonExistingResult = await authService.ForgotPasswordAsync(nonExistingEmail);

        // 4. Assert Succeeded status matches
        existingResult.Succeeded.Should().BeTrue();
        nonExistingResult.Succeeded.Should().BeTrue();

        // 5. Assert Messages are identical
        existingResult.Message.Should().Be(nonExistingResult.Message);

        // 6. Assert JSON representations of the responses are IDENTICAL
        var existingJson = JsonSerializer.Serialize(existingResult.Data);
        var nonExistingJson = JsonSerializer.Serialize(nonExistingResult.Data);

        existingJson.Should().Be(nonExistingJson, "Forgot password response body must be identical to protect against user enumeration");
        existingResult.Data!.Message.Should().Be(nonExistingResult.Data!.Message);
    }
}
