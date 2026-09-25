using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartHotel.Notifications.API.Hubs;
using SmartHotel.Notifications.API.Services;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Infrastructure.Messaging;
using SmartHotel.Notifications.Infrastructure.Persistence;
using SmartHotel.Notifications.Infrastructure.Repositories;
using SmartHotel.Notifications.Infrastructure.Services;

namespace SmartHotel.Notifications.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 1. Controllers & JSON Serialization
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // 2. Database Context
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                              ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
                              ?? "Host=postgres;Port=5432;Database=smarthotel_notifications;Username=postgres;Password=supersecret_postgres_password";

        if (builder.Environment.IsEnvironment("Testing") || string.Equals(builder.Configuration["DatabaseProvider"], "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddDbContext<NotificationsDbContext>(options =>
                options.UseInMemoryDatabase("NotificationsTestDb"));
        }
        else
        {
            builder.Services.AddDbContext<NotificationsDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        // 3. Application & Infrastructure Services
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<IChatRepository, ChatRepository>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<IChatService, ChatService>();
        builder.Services.AddSingleton<ISignalRNotificationDispatcher, SignalRNotificationDispatcher>();

        // 4. SignalR Hub & Redis Backplane
        var signalRBuilder = builder.Services.AddSignalR(hubOptions =>
        {
            hubOptions.EnableDetailedErrors = true;
        });

        var redisConn = builder.Configuration.GetConnectionString("Redis")
                        ?? builder.Configuration["Redis:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(redisConn) && !builder.Environment.IsEnvironment("Testing"))
        {
            signalRBuilder.AddStackExchangeRedis(redisConn, options =>
            {
                options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("SmartHotel.Notifications");
            });
        }

        // 5. RabbitMQ Event Consumer
        if (!builder.Environment.IsEnvironment("Testing") && !string.Equals(builder.Configuration["DisableRabbitMq"], "true", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddHostedService<NotificationEventConsumer>();
        }

        // 6. JWT Authentication with RS256 JWKS Key Resolver
        var issuer = builder.Configuration["Jwt:Issuer"] ?? "SmartHotel.Identity";
        var audience = builder.Configuration["Jwt:Audience"] ?? "SmartHotel.Clients";
        var jwksUri = builder.Configuration["Jwt:JwksUri"] ?? "http://identity-service:5001/.well-known/jwks.json";

        builder.Services.AddHttpClient("JwksClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.AddSingleton<IJwksKeyResolver, JwksKeyResolver>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;

                // Support query-string access_token for WebSockets handshake on /hubs/notifications
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwksKeyResolver>((options, keyResolver) =>
            {
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

        builder.Services.AddAuthorization();

        // 7. CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        // 8. Health Checks
        builder.Services.AddHealthChecks();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapControllers();
        app.MapHub<NotificationHub>("/hubs/notifications");

        // Apply migrations automatically in non-testing environments
        if (!app.Environment.IsEnvironment("Testing") && !string.Equals(app.Configuration["DatabaseProvider"], "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            try
            {
                db.Database.Migrate();
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Could not apply database migrations on startup (database might still be initializing).");
            }
        }

        app.Run();
    }
}

// Helper JWKS Key Resolver interface and implementation
public interface IJwksKeyResolver
{
    Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string? kid = null);
}

public class JwksKeyResolver : IJwksKeyResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _jwksUri;
    private readonly ILogger<JwksKeyResolver> _logger;
    private JsonWebKeySet? _cachedKeySet;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JwksKeyResolver(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<JwksKeyResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _jwksUri = configuration["Jwt:JwksUri"] ?? "http://identity-service:5001/.well-known/jwks.json";
        _logger = logger;
    }

    public async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string? kid = null)
    {
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
            if (_cachedKeySet != null && (DateTime.UtcNow - _lastFetch) < TimeSpan.FromMinutes(10))
            {
                var cachedKeys = _cachedKeySet.GetSigningKeys();
                if (string.IsNullOrEmpty(kid) || cachedKeys.Any(k => k.KeyId == kid))
                {
                    return string.IsNullOrEmpty(kid) ? cachedKeys : cachedKeys.Where(k => k.KeyId == kid);
                }
            }

            var client = _httpClientFactory.CreateClient("JwksClient");
            var json = await client.GetStringAsync(_jwksUri);
            _cachedKeySet = new JsonWebKeySet(json);
            _lastFetch = DateTime.UtcNow;

            var keys = _cachedKeySet.GetSigningKeys();
            return string.IsNullOrEmpty(kid) ? keys : keys.Where(k => k.KeyId == kid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve signing keys from JWKS URI {JwksUri}", _jwksUri);
            return Enumerable.Empty<SecurityKey>();
        }
        finally
        {
            _lock.Release();
        }
    }
}
