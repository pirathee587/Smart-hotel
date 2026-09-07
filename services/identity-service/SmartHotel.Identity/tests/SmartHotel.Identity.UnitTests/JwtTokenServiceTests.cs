using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Identity.UnitTests;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _jwtService;
    private readonly RsaKeyManager _keyManager;

    public JwtTokenServiceTests()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            {"Jwt:Issuer", "SmartHotel.Identity"},
            {"Jwt:Audience", "SmartHotel.Clients"},
            {"Jwt:AccessTokenExpirationMinutes", "60"},
            {"Jwt:KeyId", "test-key-id-123"},
            {"Jwt:RsaKeyPath", Path.Combine(AppContext.BaseDirectory, "test_keys", "identity_rsa.pem")}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        _keyManager = new RsaKeyManager(configuration, NullLogger<RsaKeyManager>.Instance);
        _jwtService = new JwtTokenService(_keyManager, configuration);
    }

    [Fact]
    public void GenerateAccessToken_ForEmployee_GeneratesValidRs256TokenWithExpectedClaims()
    {
        var deptId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Manager",
            Email = "john.manager@smarthotel.com",
            Role = EmployeeRole.Manager,
            DepartmentId = deptId,
            EmailVerified = true,
            IsActive = true
        };

        var tokenString = _jwtService.GenerateAccessToken(employee, mustChangePassword: false);

        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        // Algorithm must be RS256
        jwt.Header.Alg.Should().Be("RS256");
        jwt.Header.Kid.Should().Be("test-key-id-123");

        // Validate claims
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == employee.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == employee.Email);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Manager");
        jwt.Claims.Should().Contain(c => c.Type == "departmentId" && c.Value == deptId.ToString());
        jwt.Claims.Should().NotContain(c => c.Type == "must_change_password");
    }

    [Fact]
    public void GenerateAccessToken_WhenMustChangePassword_IncludesRestrictedScopeClaim()
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "New",
            LastName = "Employee",
            Email = "new.staff@smarthotel.com",
            Role = EmployeeRole.Receptionist,
            EmailVerified = true,
            IsActive = true,
            MustChangePassword = true
        };

        var tokenString = _jwtService.GenerateAccessToken(employee, mustChangePassword: true);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        jwt.Header.Alg.Should().Be("RS256");
        jwt.Claims.Should().Contain(c => c.Type == "must_change_password" && c.Value == "true");
        jwt.Claims.Should().Contain(c => c.Type == "scope" && c.Value == "change_password");
    }

    [Fact]
    public void GetJwks_ReturnsValidJwksStructureWithPublicKey()
    {
        var jwksObj = _jwtService.GetJwks();
        jwksObj.Should().NotBeNull();

        var json = System.Text.Json.JsonSerializer.Serialize(jwksObj);
        json.Should().Contain("\"kty\":\"RSA\"");
        json.Should().Contain("\"alg\":\"RS256\"");
        json.Should().Contain("\"use\":\"sig\"");
        json.Should().Contain("\"kid\":\"test-key-id-123\"");
        json.Should().Contain("\"n\":");
        json.Should().Contain("\"e\":");
    }
}
