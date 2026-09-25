using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;

namespace SmartHotel.Identity.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly RsaKeyManager _keyManager;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpirationMinutes;

    public JwtTokenService(RsaKeyManager keyManager, IConfiguration configuration)
    {
        _keyManager = keyManager;
        _issuer = configuration["Jwt:Issuer"] ?? "SmartHotel.Identity";
        _audience = configuration["Jwt:Audience"] ?? "SmartHotel.Clients";
        _accessTokenExpirationMinutes = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 60;
    }

    public string GenerateAccessToken(Person person, bool mustChangePassword = false)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, person.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, person.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (person is Employee employee)
        {
            claims.Add(new Claim(ClaimTypes.Role, employee.Role.ToString()));
            claims.Add(new Claim("role", employee.Role.ToString()));
            if (employee.DepartmentId != Guid.Empty)
            {
                claims.Add(new Claim("departmentId", employee.DepartmentId.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(employee.Department?.Name))
            {
                claims.Add(new Claim("departmentName", employee.Department.Name));
                claims.Add(new Claim("departmentCode", new string(employee.Department.Name
                    .Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray())));
            }
            if (!string.IsNullOrWhiteSpace(employee.Designation)) claims.Add(new Claim("designation", employee.Designation));
            if (employee.RequiresProfileCompletion && !employee.HasCompletedRequiredProfile())
            {
                claims.Add(new Claim("profile_incomplete", "true"));
                if (employee.ProfileCompletionDeadlineUtc.HasValue)
                    claims.Add(new Claim("profile_completion_deadline", employee.ProfileCompletionDeadlineUtc.Value.ToString("O")));
            }
        }
        else if (person is Customer customer)
        {
            claims.Add(new Claim(ClaimTypes.Role, customer.Role.ToString()));
            claims.Add(new Claim("role", customer.Role.ToString()));
        }
        else
        {
            // Fallback for any other Person subtype
            claims.Add(new Claim(ClaimTypes.Role, "Guest"));
            claims.Add(new Claim("role", "Guest"));
        }

        if (mustChangePassword)
        {
            claims.Add(new Claim("must_change_password", "true"));
            claims.Add(new Claim("scope", "change_password"));
        }

        var signingCredentials = new SigningCredentials(
            _keyManager.GetSecurityKey(),
            SecurityAlgorithms.RsaSha256);

        var expires = mustChangePassword
            ? DateTime.UtcNow.AddMinutes(15) // Short-lived temporary window
            : DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public object GetJwks() => _keyManager.GetJwks();
}
