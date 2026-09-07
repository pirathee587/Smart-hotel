using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Forwarder;

namespace SmartHotel.Gateway.IntegrationTests;

public class TestJwtHelper
{
    public RSA Rsa { get; }
    public RsaSecurityKey SecurityKey { get; }
    public string KeyId { get; } = "test-gateway-key-1";

    public TestJwtHelper()
    {
        Rsa = RSA.Create(2048);
        SecurityKey = new RsaSecurityKey(Rsa) { KeyId = KeyId };
    }

    public string GenerateToken(string sub, string role = "Customer", TimeSpan? lifetime = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sub),
            new("sub", sub),
            new(ClaimTypes.Role, role),
            new("role", role),
            new("jti", Guid.NewGuid().ToString())
        };

        var creds = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
            Issuer = "SmartHotel.Identity",
            Audience = "SmartHotel.Clients",
            SigningCredentials = creds
        };

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }
}

public class TestForwarderHttpClientFactory : IForwarderHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public TestForwarderHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        return new HttpMessageInvoker(_handler);
    }
}

public class MockDownstreamHandler : HttpMessageHandler
{
    public SmartHotel.Identity.Infrastructure.Services.InMemoryRateLimiterService IdentityRateLimiter { get; } = new();

    public class MockRoomType
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
    }

    public List<MockRoomType> RoomTypes { get; } = new()
    {
        new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333331"), Name = "Deluxe Ocean Suite", IsPublished = true },
        new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333332"), Name = "Executive City Room", IsPublished = true }
    };

    public TestJwtHelper? JwtHelper { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        if (path.EndsWith("/health"))
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"Healthy\"}", System.Text.Encoding.UTF8, "application/json")
            };
        }

        if (path.Contains("/api/v1/customers/register"))
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"customerId\":\"" + Guid.NewGuid() + "\",\"status\":\"Registered\"}", System.Text.Encoding.UTF8, "application/json")
            };
        }

        if (path.Contains("/api/v1/customers/login"))
        {
            var token = JwtHelper?.GenerateToken("cust-001", "Customer") ?? "token";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"accessToken\":\"" + token + "\",\"tokenType\":\"Bearer\",\"expiresIn\":3600}", System.Text.Encoding.UTF8, "application/json")
            };
        }

        if (path.Contains("/api/v1/auth/employee/login"))
        {
            var token = JwtHelper?.GenerateToken("admin-001", "Admin") ?? "token";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"accessToken\":\"" + token + "\",\"tokenType\":\"Bearer\",\"expiresIn\":3600}", System.Text.Encoding.UTF8, "application/json")
            };
        }

        if (path.Contains("/api/v1/auth/forgot-password"))
        {
            string email = "test@smarthotel.com";
            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("email", out var emailProp))
                    {
                        email = emailProp.GetString() ?? email;
                    }
                }
                catch { }
            }

            var allowed = IdentityRateLimiter.CheckAndRecordAttempt($"forgot-password:{email.Trim().ToLowerInvariant()}", maxAttempts: 5, window: TimeSpan.FromHours(1));
            if (!allowed)
            {
                return new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = new StringContent("{\"message\":\"Too many password reset requests. Please try again later.\"}", System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":\"If the account exists, a password reset link has been sent.\"}", System.Text.Encoding.UTF8, "application/json")
            };
        }

        if (path.Contains("/api/v1/auth/me"))
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":\"User profile retrieved successfully\"}", System.Text.Encoding.UTF8, "application/json")
            };
        }

        // RoomTypes Endpoints
        if (path.Contains("/api/v1/room-types"))
        {
            // Extract role if token provided
            string role = "Anonymous";
            var authHeader = request.Headers.Authorization;
            if (authHeader != null && authHeader.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            {
                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                if (tokenHandler.CanReadToken(authHeader.Parameter))
                {
                    var jwt = tokenHandler.ReadJwtToken(authHeader.Parameter);
                    role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value ?? "Customer";
                }
            }

            // Publish endpoint: POST /api/v1/room-types/{id}/publish
            if (path.EndsWith("/publish", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = new StringContent("{\"message\":\"Forbidden: Admin role required\"}", System.Text.Encoding.UTF8, "application/json")
                    };
                }

                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && Guid.TryParse(segments[^2], out var targetId))
                {
                    var rt = RoomTypes.FirstOrDefault(r => r.Id == targetId);
                    if (rt != null)
                    {
                        rt.IsPublished = true;
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"message\":\"Room type published successfully.\"}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"message\":\"Room type not found.\"}", System.Text.Encoding.UTF8, "application/json")
                };
            }

            // GET list of room types
            if (request.Method == HttpMethod.Get)
            {
                bool isStaff = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);

                var list = isStaff ? RoomTypes : RoomTypes.Where(r => r.IsPublished).ToList();
                var json = System.Text.Json.JsonSerializer.Serialize(list);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            // POST create new room type
            if (request.Method == HttpMethod.Post)
            {
                if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = new StringContent("{\"message\":\"Forbidden: Admin role required\"}", System.Text.Encoding.UTF8, "application/json")
                    };
                }

                string name = "New Penthouse";
                if (request.Content != null)
                {
                    var body = await request.Content.ReadAsStringAsync(cancellationToken);
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("name", out var nameProp))
                        {
                            name = nameProp.GetString() ?? name;
                        }
                    }
                    catch { }
                }

                var newRt = new MockRoomType
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    IsPublished = false // Starts in draft state
                };
                RoomTypes.Add(newRt);

                var json = System.Text.Json.JsonSerializer.Serialize(newRt);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"downstream\":true}", System.Text.Encoding.UTF8, "application/json")
        };
    }
}

public class CustomGatewayFactory : WebApplicationFactory<Program>
{
    public TestJwtHelper JwtHelper { get; } = new();
    public MockDownstreamHandler DownstreamHandler { get; } = new();

    public CustomGatewayFactory()
    {
        DownstreamHandler.JwtHelper = JwtHelper;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Configure tight rate limits for quick test verification
            var testConfig = new Dictionary<string, string?>
            {
                ["RateLimiting:Customer:TokenLimit"] = "3",
                ["RateLimiting:Customer:TokensPerPeriod"] = "3",
                ["RateLimiting:Customer:ReplenishmentPeriodSeconds"] = "60",
                ["RateLimiting:Customer:QueueLimit"] = "0",

                ["RateLimiting:Employee:TokenLimit"] = "5",
                ["RateLimiting:Employee:TokensPerPeriod"] = "5",
                ["RateLimiting:Employee:ReplenishmentPeriodSeconds"] = "60",
                ["RateLimiting:Employee:QueueLimit"] = "0",

                ["RateLimiting:Admin:TokenLimit"] = "10",
                ["RateLimiting:Admin:TokensPerPeriod"] = "10",
                ["RateLimiting:Admin:ReplenishmentPeriodSeconds"] = "60",
                ["RateLimiting:Admin:QueueLimit"] = "0",

                ["RateLimiting:Anonymous:TokenLimit"] = "2",
                ["RateLimiting:Anonymous:TokensPerPeriod"] = "2",
                ["RateLimiting:Anonymous:ReplenishmentPeriodSeconds"] = "60",
                ["RateLimiting:Anonymous:QueueLimit"] = "0"
            };
            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace JWKS key resolver with test resolver
            services.AddSingleton<IJwksKeyResolver>(new TestJwksKeyResolver(JwtHelper.SecurityKey));

            // Replace YARP downstream HTTP client with mock handler
            services.AddSingleton<IForwarderHttpClientFactory>(new TestForwarderHttpClientFactory(DownstreamHandler));

            // Replace health check HTTP client factory
            services.AddHttpClient("HealthChecks").ConfigurePrimaryHttpMessageHandler(() => DownstreamHandler);
        });
    }
}

public class TestJwksKeyResolver : IJwksKeyResolver
{
    private readonly SecurityKey _key;
    public TestJwksKeyResolver(SecurityKey key) => _key = key;

    public Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string? kid = null)
    {
        return Task.FromResult<IEnumerable<SecurityKey>>(new[] { _key });
    }
}
