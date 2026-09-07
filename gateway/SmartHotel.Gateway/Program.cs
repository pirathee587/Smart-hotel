using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SmartHotel.Gateway;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ---------------------------------------------------------------------
        // 1. Configuration & Options
        // ---------------------------------------------------------------------
        builder.Services.Configure<GatewayRateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));

        var issuer = builder.Configuration["Authentication:Issuer"] ?? "SmartHotel.Identity";
        var audience = builder.Configuration["Authentication:Audience"] ?? "SmartHotel.Clients";
        var jwksUri = builder.Configuration["Authentication:JwksUri"] ?? "http://identity-service:5001/.well-known/jwks.json";

        // ---------------------------------------------------------------------
        // 2. RS256 JWKS-Aware Key Resolver & JWT Authentication Middleware
        // ---------------------------------------------------------------------
        builder.Services.AddHttpClient<IJwksKeyResolver, JwksKeyResolver>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwksKeyResolver>((options, keyResolver) =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                    {
                        return keyResolver.GetSigningKeysAsync(kid).GetAwaiter().GetResult();
                    }
                };
            });

        // ---------------------------------------------------------------------
        // 3. Authorization Policies
        // ---------------------------------------------------------------------
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        // ---------------------------------------------------------------------
        // 4. JWT-Claim-Based Rate Limiting (.NET 8 System.Threading.RateLimiting)
        // Partitioned per-service (clusterId) to prevent cross-service starvation
        // ---------------------------------------------------------------------
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                string retryAfterSeconds = "60";
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = Math.Max(1, (int)retryAfter.TotalSeconds).ToString();
                }

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds;
                context.HttpContext.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    error = "Too Many Requests",
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfter = retryAfterSeconds
                };
                await context.HttpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken: token);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var rateLimitOptions = httpContext.RequestServices.GetService<IOptions<GatewayRateLimitingOptions>>()?.Value ?? new GatewayRateLimitingOptions();
                var targetService = GetTargetServiceCluster(httpContext);
                var user = httpContext.User;
                if (user.Identity?.IsAuthenticated == true)
                {
                    var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value;

                    if (!string.IsNullOrEmpty(sub))
                    {
                        var role = user.FindFirst(ClaimTypes.Role)?.Value
                                   ?? user.FindFirst("role")?.Value
                                   ?? "Customer";

                        var tier = GetRateLimitTier(role, rateLimitOptions);
                        return RateLimitPartition.GetTokenBucketLimiter(
                            $"user:{sub}:{targetService}",
                            _ => new TokenBucketRateLimiterOptions
                            {
                                TokenLimit = tier.TokenLimit,
                                TokensPerPeriod = tier.TokensPerPeriod,
                                ReplenishmentPeriod = TimeSpan.FromSeconds(tier.ReplenishmentPeriodSeconds),
                                QueueLimit = tier.QueueLimit,
                                AutoReplenishment = true
                            });
                    }
                }

                // Fallback for anonymous / public endpoints: partition by client IP and target service
                string clientIp = "unknown";
                if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
                {
                    clientIp = forwardedFor.ToString().Split(',')[0].Trim();
                }
                else if (httpContext.Connection.RemoteIpAddress != null)
                {
                    clientIp = httpContext.Connection.RemoteIpAddress.ToString();
                }

                var anonTier = rateLimitOptions.Anonymous;
                return RateLimitPartition.GetTokenBucketLimiter(
                    $"ip:{clientIp}:{targetService}",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = anonTier.TokenLimit,
                        TokensPerPeriod = anonTier.TokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(anonTier.ReplenishmentPeriodSeconds),
                        QueueLimit = anonTier.QueueLimit,
                        AutoReplenishment = true
                    });
            });
        });

        // ---------------------------------------------------------------------
        // 5. Downstream Health Check Aggregation
        // ---------------------------------------------------------------------
        builder.Services.AddHttpClient("HealthChecks", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        builder.Services.AddHealthChecks()
            .AddCheck<IdentityServiceHealthCheck>("identity-service")
            .AddCheck<HotelOpsServiceHealthCheck>("hotel-ops-service")
            .AddCheck<BookingServiceHealthCheck>("booking-service")
            .AddCheck<FieldOpsServiceHealthCheck>("field-ops-service")
            .AddCheck<NotificationServiceHealthCheck>("notifications-service")
            .AddCheck<ConciergeServiceHealthCheck>("concierge-service");

        // ---------------------------------------------------------------------
        // 6. YARP Reverse Proxy
        // ---------------------------------------------------------------------
        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        // ---------------------------------------------------------------------
        // 7. Request Pipeline Configuration
        // ---------------------------------------------------------------------
        app.UseCors();

        // Authentication validates JWT; Authorization blocks unauthorized access to protected routes
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        // Health check aggregation endpoint
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    gateway = "Healthy",
                    downstream = report.Entries.ToDictionary(
                        e => e.Key,
                        e => new
                        {
                            status = e.Value.Status.ToString(),
                            description = e.Value.Description,
                            durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2)
                        }),
                    totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2)
                };
                await context.Response.WriteAsJsonAsync(response);
            }
        }).AllowAnonymous();

        // YARP Reverse Proxy Route Mapping
        app.MapReverseProxy();

        app.Run();
    }

    private static string GetTargetServiceCluster(HttpContext context)
    {
        var routeModel = context.GetEndpoint()?.Metadata.GetMetadata<Yarp.ReverseProxy.Model.RouteModel>();
        if (!string.IsNullOrEmpty(routeModel?.Config.ClusterId))
        {
            return routeModel.Config.ClusterId;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.StartsWith("/api/v1/auth") || path.StartsWith("/api/v1/customers") || path.StartsWith("/api/v1/employees") || path.StartsWith("/api/v1/portal-auth") || path.StartsWith("/.well-known"))
        {
            return "identity-service";
        }
        if (path.StartsWith("/api/v1/room-types") || path.StartsWith("/api/v1/rooms") || path.StartsWith("/api/v1/hotels") || path.StartsWith("/api/v1/departments"))
        {
            return "hotelops-service";
        }
        if (path.StartsWith("/api/v1/bookings") || path.StartsWith("/api/v1/payments") || path.StartsWith("/api/v1/kiosk") || path.StartsWith("/api/v1/reviews") || path.StartsWith("/api/v1/complaints"))
        {
            return "booking-service";
        }
        if (path.StartsWith("/api/v1/tasks") || path.StartsWith("/api/v1/kds") || path.StartsWith("/api/v1/attendance") || path.StartsWith("/api/v1/payroll"))
        {
            return "fieldops-service";
        }
        if (path.StartsWith("/api/v1/notifications") || path.StartsWith("/api/v1/chat") || path.StartsWith("/hubs/notifications"))
        {
            return "notifications-service";
        }
        if (path.StartsWith("/api/v1/concierge"))
        {
            return "concierge-service";
        }

        return "default";
    }

    private static RateLimitTierOptions GetRateLimitTier(string role, GatewayRateLimitingOptions options)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return options.Admin;
        }
        if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return options.Customer;
        }
        // All internal employee roles: Manager, Receptionist, Housekeeper, Maintenance, Chef, Waiter, Security, Employee
        return options.Employee;
    }
}

// -----------------------------------------------------------------------------
// JWKS Key Resolver Interface and Implementation
// -----------------------------------------------------------------------------
public interface IJwksKeyResolver
{
    Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string? kid = null);
}

public class JwksKeyResolver : IJwksKeyResolver
{
    private readonly HttpClient _httpClient;
    private readonly string _jwksUri;
    private readonly ILogger<JwksKeyResolver> _logger;
    private JsonWebKeySet? _cachedKeySet;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JwksKeyResolver(HttpClient httpClient, IConfiguration configuration, ILogger<JwksKeyResolver> logger)
    {
        _httpClient = httpClient;
        _jwksUri = configuration["Authentication:JwksUri"] ?? "http://identity-service:5001/.well-known/jwks.json";
        _logger = logger;
    }

    public async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string? kid = null)
    {
        // Check cache (refresh after 10 mins or if kid not found)
        if (_cachedKeySet != null && (DateTime.UtcNow - _lastFetch) < TimeSpan.FromMinutes(10))
        {
            var cachedKeys = _cachedKeySet.GetSigningKeys();
            if (string.IsNullOrEmpty(kid) || cachedKeys.Any(k => k.KeyId == kid))
            {
                return string.IsNullOrEmpty(kid) ? cachedKeys : cachedKeys.Where(k => k.KeyId == kid);
            }
        }

        await _lock.WaitAsync();
        try
        {
            // Re-check cache inside lock
            if (_cachedKeySet != null && (DateTime.UtcNow - _lastFetch) < TimeSpan.FromSeconds(30))
            {
                var cachedKeys = _cachedKeySet.GetSigningKeys();
                if (string.IsNullOrEmpty(kid) || cachedKeys.Any(k => k.KeyId == kid))
                {
                    return string.IsNullOrEmpty(kid) ? cachedKeys : cachedKeys.Where(k => k.KeyId == kid);
                }
            }

            _logger.LogInformation("Fetching fresh JWKS from {JwksUri}", _jwksUri);
            var response = await _httpClient.GetStringAsync(_jwksUri);
            _cachedKeySet = new JsonWebKeySet(response);
            _lastFetch = DateTime.UtcNow;

            var keys = _cachedKeySet.GetSigningKeys();
            return string.IsNullOrEmpty(kid) ? keys : keys.Where(k => k.KeyId == kid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch JWKS from {JwksUri}. Using cached keys if available.", _jwksUri);
            var fallback = _cachedKeySet?.GetSigningKeys() ?? Enumerable.Empty<SecurityKey>();
            return string.IsNullOrEmpty(kid) ? fallback : fallback.Where(k => k.KeyId == kid);
        }
        finally
        {
            _lock.Release();
        }
    }
}

// -----------------------------------------------------------------------------
// Supporting Options & Health Check Types
// -----------------------------------------------------------------------------
public class RateLimitTierOptions
{
    public int TokenLimit { get; set; } = 60;
    public int TokensPerPeriod { get; set; } = 60;
    public int ReplenishmentPeriodSeconds { get; set; } = 60;
    public int QueueLimit { get; set; } = 0;
}

public class GatewayRateLimitingOptions
{
    public RateLimitTierOptions Customer { get; set; } = new() { TokenLimit = 60, TokensPerPeriod = 60, ReplenishmentPeriodSeconds = 60, QueueLimit = 0 };
    public RateLimitTierOptions Employee { get; set; } = new() { TokenLimit = 120, TokensPerPeriod = 120, ReplenishmentPeriodSeconds = 60, QueueLimit = 0 };
    public RateLimitTierOptions Admin { get; set; } = new() { TokenLimit = 200, TokensPerPeriod = 200, ReplenishmentPeriodSeconds = 60, QueueLimit = 0 };
    public RateLimitTierOptions Anonymous { get; set; } = new() { TokenLimit = 30, TokensPerPeriod = 30, ReplenishmentPeriodSeconds = 60, QueueLimit = 0 };
}

public class IdentityServiceHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentityServiceHealthCheck> _logger;

    public IdentityServiceHealthCheck(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        ILogger<IdentityServiceHealthCheck> logger)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var identityUrl = _configuration["DownstreamServices:IdentityService"] ?? "http://identity-service:5001";
        var healthEndpoint = $"{identityUrl.TrimEnd('/')}/health";

        try
        {
            var client = _clientFactory.CreateClient("HealthChecks");
            var response = await client.GetAsync(healthEndpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Identity Service reachable at {healthEndpoint}");
            }
            return HealthCheckResult.Degraded($"Identity Service returned status code {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Identity Service health endpoint at {HealthEndpoint}", healthEndpoint);
            return HealthCheckResult.Unhealthy($"Identity Service unreachable at {healthEndpoint}: {ex.Message}");
        }
    }
}

public class HotelOpsServiceHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HotelOpsServiceHealthCheck> _logger;

    public HotelOpsServiceHealthCheck(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        ILogger<HotelOpsServiceHealthCheck> logger)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var hotelOpsUrl = _configuration["DownstreamServices:HotelOpsService"] ?? "http://hotel-ops-service:5002";
        var healthEndpoint = $"{hotelOpsUrl.TrimEnd('/')}/health";

        try
        {
            var client = _clientFactory.CreateClient("HealthChecks");
            var response = await client.GetAsync(healthEndpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Hotel Ops Service reachable at {healthEndpoint}");
            }
            return HealthCheckResult.Degraded($"Hotel Ops Service returned status code {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Hotel Ops Service health endpoint at {HealthEndpoint}", healthEndpoint);
            return HealthCheckResult.Unhealthy($"Hotel Ops Service unreachable at {healthEndpoint}: {ex.Message}");
        }
    }
}

public class BookingServiceHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BookingServiceHealthCheck> _logger;

    public BookingServiceHealthCheck(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        ILogger<BookingServiceHealthCheck> logger)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var bookingUrl = _configuration["DownstreamServices:BookingService"] ?? "http://booking-payments-service:5003";
        var healthEndpoint = $"{bookingUrl.TrimEnd('/')}/health";

        try
        {
            var client = _clientFactory.CreateClient("HealthChecks");
            var response = await client.GetAsync(healthEndpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Booking Service reachable at {healthEndpoint}");
            }
            return HealthCheckResult.Degraded($"Booking Service returned status code {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Booking Service health endpoint at {HealthEndpoint}", healthEndpoint);
            return HealthCheckResult.Unhealthy($"Booking Service unreachable at {healthEndpoint}: {ex.Message}");
        }
    }
}

public class FieldOpsServiceHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FieldOpsServiceHealthCheck> _logger;

    public FieldOpsServiceHealthCheck(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        ILogger<FieldOpsServiceHealthCheck> logger)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var fieldOpsUrl = _configuration["DownstreamServices:FieldOpsService"] ?? "http://field-ops-service:8084";
        var healthEndpoint = $"{fieldOpsUrl.TrimEnd('/')}/actuator/health";

        try
        {
            var client = _clientFactory.CreateClient("HealthChecks");
            var response = await client.GetAsync(healthEndpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Field Ops Service reachable at {healthEndpoint}");
            }
            return HealthCheckResult.Degraded($"Field Ops Service returned status code {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Field Ops Service health endpoint at {HealthEndpoint}", healthEndpoint);
            return HealthCheckResult.Unhealthy($"Field Ops Service unreachable at {healthEndpoint}: {ex.Message}");
        }
    }
}

public class NotificationServiceHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationServiceHealthCheck> _logger;

    public NotificationServiceHealthCheck(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        ILogger<NotificationServiceHealthCheck> logger)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var notifUrl = _configuration["DownstreamServices:NotificationService"] ?? "http://notification-service:5004";
        var healthEndpoint = $"{notifUrl.TrimEnd('/')}/health";

        try
        {
            var client = _clientFactory.CreateClient("HealthChecks");
            var response = await client.GetAsync(healthEndpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Notification Service reachable at {healthEndpoint}");
            }
            return HealthCheckResult.Degraded($"Notification Service returned status code {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Notification Service health endpoint at {HealthEndpoint}", healthEndpoint);
            return HealthCheckResult.Unhealthy($"Notification Service unreachable at {healthEndpoint}: {ex.Message}");
        }
    }
}

public class ConciergeServiceHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConciergeServiceHealthCheck> _logger;

    public ConciergeServiceHealthCheck(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        ILogger<ConciergeServiceHealthCheck> logger)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var conciergeUrl = _configuration["DownstreamServices:ConciergeService"] ?? "http://concierge-service:5005";
        var healthEndpoint = $"{conciergeUrl.TrimEnd('/')}/health";

        try
        {
            var client = _clientFactory.CreateClient("HealthChecks");
            var response = await client.GetAsync(healthEndpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Concierge Service reachable at {healthEndpoint}");
            }
            return HealthCheckResult.Degraded($"Concierge Service returned status code {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Concierge Service health endpoint at {HealthEndpoint}", healthEndpoint);
            return HealthCheckResult.Unhealthy($"Concierge Service unreachable at {healthEndpoint}: {ex.Message}");
        }
    }
}
