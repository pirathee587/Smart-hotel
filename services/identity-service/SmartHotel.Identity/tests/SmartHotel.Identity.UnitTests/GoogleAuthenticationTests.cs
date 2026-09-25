using System.IdentityModel.Tokens.Jwt;
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

public class GoogleAuthenticationTests
{
    private (AppDbContext dbContext, AuthService authService, JwtTokenService jwtTokenService) CreateTestContext()
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
            {"Jwt:RsaKeyPath", Path.Combine(AppContext.BaseDirectory, "test_keys", "identity_rsa.pem")},
            {"Authentication:Google:ClientId", "mock-google-client-id.apps.googleusercontent.com"}
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
            rateLimiter,
            configuration);

        return (dbContext, authService, jwtTokenService);
    }

    [Fact]
    public async Task GoogleLogin_EmptyToken_ReturnsFailure()
    {
        var (_, authService, _) = CreateTestContext();

        var result = await authService.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "" });

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("required");
    }

    [Fact]
    public async Task GoogleLogin_UnregisteredUser_RejectsLoginWithExactMessage()
    {
        var (dbContext, authService, _) = CreateTestContext();

        // Database has NO user with this email
        var request = new GoogleLoginRequest
        {
            IdToken = "demo_google_token_123:unregistered.guest@example.com"
        };

        var result = await authService.GoogleLoginAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be("Your Google account is not registered in the SmartHotel system. Please contact an administrator.");

        // Must NOT create an account
        var count = await dbContext.Persons.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task GoogleLogin_ExistingAdmin_GeneratesSmartHotelJwtWithAdminRole()
    {
        var (dbContext, authService, _) = CreateTestContext();

        var adminEmail = "admin@smarthotel.com";
        var admin = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Admin",
            LastName = "User",
            Email = adminEmail,
            PasswordHash = "hash123",
            Role = EmployeeRole.Admin,
            IsActive = true,
            EmailVerified = true,
            MustChangePassword = false
        };
        dbContext.Employees.Add(admin);
        await dbContext.SaveChangesAsync();

        var request = new GoogleLoginRequest
        {
            IdToken = $"demo_google_token_123:{adminEmail}"
        };

        var result = await authService.GoogleLoginAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Token.Should().NotBeNullOrWhiteSpace();
        result.Data.User.Role.Should().Be("Admin");
        result.Data.User.Email.Should().Be(adminEmail);
        result.Data.User.Name.Should().Be("Admin User");

        // Inspect JWT claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Data.Token);
        jwt.Header.Alg.Should().Be("RS256");
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Admin");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == adminEmail);
    }

    [Fact]
    public async Task GoogleLogin_ExistingManagerWithDepartment_GeneratesJwtWithDepartmentClaim()
    {
        var (dbContext, authService, _) = CreateTestContext();

        var deptId = Guid.NewGuid();
        var managerEmail = "manager@smarthotel.com";
        var manager = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Manager",
            LastName = "Person",
            Email = managerEmail,
            PasswordHash = "hash123",
            Role = EmployeeRole.Manager,
            DepartmentId = deptId,
            IsActive = true,
            EmailVerified = true
        };
        dbContext.Employees.Add(manager);
        await dbContext.SaveChangesAsync();

        var request = new GoogleLoginRequest
        {
            IdToken = $"demo_google_token_123:{managerEmail}"
        };

        var result = await authService.GoogleLoginAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.User.Role.Should().Be("Manager");
        result.Data.User.DepartmentId.Should().Be(deptId);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Data.Token);
        jwt.Claims.Should().Contain(c => c.Type == "departmentId" && c.Value == deptId.ToString());
    }

    [Fact]
    public async Task GoogleLogin_ExistingCustomer_GeneratesSmartHotelJwtWithCustomerRole()
    {
        var (dbContext, authService, _) = CreateTestContext();

        var customerEmail = "guest@example.com";
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Doe",
            Email = customerEmail,
            PasswordHash = "hash123",
            IsActive = true,
            EmailVerified = true
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var request = new GoogleLoginRequest
        {
            IdToken = $"demo_google_token_123:{customerEmail}"
        };

        var result = await authService.GoogleLoginAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.User.Role.Should().Be("Guest");
        result.Data.User.Email.Should().Be(customerEmail);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Data.Token);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Guest");
    }

    [Fact]
    public async Task GoogleLogin_InactiveAccount_RejectsLogin()
    {
        var (dbContext, authService, _) = CreateTestContext();

        var email = "deactivated.staff@smarthotel.com";
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Former",
            LastName = "Staff",
            Email = email,
            PasswordHash = "hash123",
            Role = EmployeeRole.Receptionist,
            IsActive = false,
            EmailVerified = true
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var request = new GoogleLoginRequest
        {
            IdToken = $"demo_google_token_123:{email}"
        };

        var result = await authService.GoogleLoginAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("deactivated");
    }
}
