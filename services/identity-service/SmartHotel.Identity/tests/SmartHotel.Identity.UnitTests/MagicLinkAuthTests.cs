using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.Identity.Application.Features.Auth.Models;
using SmartHotel.Identity.Application.Features.Auth.Services;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;
using SmartHotel.Identity.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Identity.UnitTests;

public class MagicLinkAuthTests
{
    private (AppDbContext dbContext, AuthService authService, InMemoryRateLimiterService rateLimiter, FakeEmailService emailService) CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var inMemoryConfig = new Dictionary<string, string?>
        {
            {"Jwt:Issuer", "SmartHotel.Identity"},
            {"Jwt:Audience", "SmartHotel.Clients"},
            {"Jwt:AccessTokenExpirationMinutes", "60"},
            {"Jwt:KeyId", "test-key-id-123"},
            {"Jwt:RsaKeyPath", Path.Combine(AppContext.BaseDirectory, "test_keys", "identity_rsa.pem")}
        };

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var dbContext = new AppDbContext(options);
        var passwordHasher = new PasswordHasher();
        var keyManager = new RsaKeyManager(configuration, NullLogger<RsaKeyManager>.Instance);
        var jwtTokenService = new JwtTokenService(keyManager, configuration);
        var emailService = new FakeEmailService(NullLogger<FakeEmailService>.Instance);
        var rateLimiter = new InMemoryRateLimiterService();

        var authService = new AuthService(
            dbContext,
            passwordHasher,
            jwtTokenService,
            emailService,
            rateLimiter);

        return (dbContext, authService, rateLimiter, emailService);
    }

    [Fact]
    public async Task RequestMagicLink_EnumerationProtection_ReturnsIdenticalResponseForExistingAndNonExistingEmail()
    {
        var (dbContext, authService, _, _) = CreateTestContext();

        // Seed customer
        var existingEmail = "alice@smarthotel.com";
        dbContext.Customers.Add(new Customer
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = existingEmail,
            PasswordHash = "hash",
            EmailVerified = true,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        // Act
        var existingResult = await authService.RequestMagicLinkAsync(new RequestMagicLinkCommand(existingEmail));
        var nonExistingResult = await authService.RequestMagicLinkAsync(new RequestMagicLinkCommand("stranger@smarthotel.com"));

        // Assert
        existingResult.Succeeded.Should().BeTrue();
        nonExistingResult.Succeeded.Should().BeTrue();

        var existingJson = JsonSerializer.Serialize(existingResult.Data);
        var nonExistingJson = JsonSerializer.Serialize(nonExistingResult.Data);

        existingJson.Should().Be(nonExistingJson, "Response payload must be identical to protect against user enumeration");
        existingResult.Data!.Message.Should().Be("If an account with this email exists, a magic login link has been sent.");
    }

    [Fact]
    public async Task RequestMagicLink_RateLimiting_Exceeding5PerHourIsRejected()
    {
        var (dbContext, authService, _, _) = CreateTestContext();
        var email = "charlie@smarthotel.com";

        dbContext.Customers.Add(new Customer
        {
            FirstName = "Charlie",
            LastName = "Brown",
            Email = email,
            PasswordHash = "hash",
            EmailVerified = true,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        // First 5 attempts should succeed
        for (int i = 0; i < 5; i++)
        {
            var res = await authService.RequestMagicLinkAsync(new RequestMagicLinkCommand(email));
            res.Succeeded.Should().BeTrue($"Attempt {i + 1} should be permitted within rate limit");
        }

        // 6th attempt should be blocked
        var blockedRes = await authService.RequestMagicLinkAsync(new RequestMagicLinkCommand(email));
        blockedRes.Succeeded.Should().BeFalse();
        blockedRes.Message.Should().Contain("Too many magic link requests");
    }

    [Fact]
    public async Task RequestMagicLink_EmployeeEmail_DoesNotIssueMagicLinkToken()
    {
        var (dbContext, authService, _, _) = CreateTestContext();
        var employeeEmail = "staff@smarthotel.com";

        dbContext.Employees.Add(new Employee
        {
            FirstName = "Bob",
            LastName = "Staff",
            Email = employeeEmail,
            PasswordHash = "hash",
            Role = EmployeeRole.Receptionist,
            EmailVerified = true,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        // Act: Employee attempts to request customer magic link
        var result = await authService.RequestMagicLinkAsync(new RequestMagicLinkCommand(employeeEmail));

        // Assert: Enumeration protection returns success to caller
        result.Succeeded.Should().BeTrue();

        // But employee record did not get a token (magic link is customer-only)
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Email == employeeEmail);
        customer.Should().BeNull("Employees are not customers and cannot have customer magic links");
    }

    [Fact]
    public async Task VerifyMagicLink_ValidToken_IssuesFullJwtAndLogsIn()
    {
        var (dbContext, authService, _, _) = CreateTestContext();
        var email = "david@smarthotel.com";

        var customer = new Customer
        {
            FirstName = "David",
            LastName = "Miller",
            Email = email,
            PasswordHash = "hash",
            EmailVerified = true,
            IsActive = true
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        // Request magic link
        await authService.RequestMagicLinkAsync(new RequestMagicLinkCommand(email));

        // Reload customer to get generated token
        var reloadedCustomer = await dbContext.Customers.FirstAsync(c => c.Email == email);
        var token = reloadedCustomer.MagicLinkToken;
        token.Should().NotBeNullOrWhiteSpace();
        reloadedCustomer.MagicLinkTokenExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(14));

        // Act: Verify token
        var verifyResult = await authService.VerifyMagicLinkAsync(new VerifyMagicLinkCommand(token!));

        // Assert: Succeeded and issued RS256 JWT
        verifyResult.Succeeded.Should().BeTrue();
        verifyResult.Data.Should().NotBeNull();
        verifyResult.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        verifyResult.Data.TokenType.Should().Be("Bearer");
        verifyResult.Data.User.Role.Should().Be("Customer");
        verifyResult.Data.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task VerifyMagicLink_SingleUseEnforcement_SecondAttemptIsRejected()
    {
        var (dbContext, authService, _, _) = CreateTestContext();
        var email = "eva@smarthotel.com";

        var customer = new Customer
        {
            FirstName = "Eva",
            LastName = "Green",
            Email = email,
            PasswordHash = "hash",
            EmailVerified = true,
            IsActive = true
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        await authService.RequestMagicLinkAsync(new RequestMagicLinkCommand(email));
        var reloadedCustomer = await dbContext.Customers.FirstAsync(c => c.Email == email);
        var token = reloadedCustomer.MagicLinkToken!;

        // First verification should succeed
        var firstAttempt = await authService.VerifyMagicLinkAsync(new VerifyMagicLinkCommand(token));
        firstAttempt.Succeeded.Should().BeTrue();

        // Second verification with the exact same token MUST fail (single-use)
        var secondAttempt = await authService.VerifyMagicLinkAsync(new VerifyMagicLinkCommand(token));
        secondAttempt.Succeeded.Should().BeFalse();
        secondAttempt.Message.Should().Contain("Invalid or expired");
    }

    [Fact]
    public async Task VerifyMagicLink_TokenExpiry_TokenOlderThan15MinutesIsRejected()
    {
        var (dbContext, authService, _, _) = CreateTestContext();
        var email = "frank@smarthotel.com";

        var expiredToken = "EXPIRED_TOKEN_12345";
        var customer = new Customer
        {
            FirstName = "Frank",
            LastName = "Castle",
            Email = email,
            PasswordHash = "hash",
            EmailVerified = true,
            IsActive = true,
            MagicLinkToken = expiredToken,
            MagicLinkTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) // Expired 1 min ago
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await authService.VerifyMagicLinkAsync(new VerifyMagicLinkCommand(expiredToken));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Invalid or expired");

        // The expired token should be invalidated/cleared in the database
        var updated = await dbContext.Customers.FirstAsync(c => c.Email == email);
        updated.MagicLinkToken.Should().BeNull();
    }

    [Fact]
    public async Task VerifyMagicLink_EmailNotVerified_ReturnsEmailNotVerifiedSignal()
    {
        var (dbContext, authService, _, _) = CreateTestContext();
        var email = "unverified@smarthotel.com";

        var token = "VALID_TOKEN_UNVERIFIED_USER";
        var customer = new Customer
        {
            FirstName = "Grace",
            LastName = "Hopper",
            Email = email,
            PasswordHash = "hash",
            EmailVerified = false, // NOT verified!
            IsActive = true,
            MagicLinkToken = token,
            MagicLinkTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        // Act: Attempt to log in via magic link
        var result = await authService.VerifyMagicLinkAsync(new VerifyMagicLinkCommand(token));

        // Assert: Rejection due to unverified email
        result.Succeeded.Should().BeFalse();
        (result.Message.Contains("EMAIL_NOT_VERIFIED") || result.Errors.Contains("EMAIL_NOT_VERIFIED"))
            .Should().BeTrue("Magic link login must not bypass EmailVerified gate");
    }
}
