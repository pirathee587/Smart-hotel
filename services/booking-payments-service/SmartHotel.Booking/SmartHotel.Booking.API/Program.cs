using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartHotel.Booking.API.Services;
using SmartHotel.Booking.Application;
using SmartHotel.Booking.Infrastructure;
using SmartHotel.Booking.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Configure dual-port Kestrel: HTTP/1.1 for REST, HTTP/2 cleartext (h2c) for gRPC
builder.WebHost.ConfigureKestrel(options =>
{
    var restPort = builder.Configuration.GetValue<int?>("BOOKING_HTTP_PORT") ?? builder.Configuration.GetValue<int?>("PORT") ?? 5003;
    var grpcPort = builder.Configuration.GetValue<int?>("BOOKING_GRPC_PORT") ?? builder.Configuration.GetValue<int?>("GRPC_PORT") ?? 5013;

    options.Listen(IPAddress.Any, restPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });

    options.Listen(IPAddress.Any, grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// 1. Add services to container
builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

// 2. Swagger with Bearer token authentication
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartHotel Booking & Payments Service API",
        Version = "v1",
        Description = "Reservations, PayHere Payments, Concurrency Protection, Kiosk, Reviews & Complaints Service (RS256 JWT, CQRS/MediatR)"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter 'Bearer' [space] and then your valid RS256 token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

// 3. Clean Architecture layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// 4. RS256 JWT Authentication configuration with resilient JWKS Key Resolver
var issuer = builder.Configuration["Jwt:Issuer"] ?? builder.Configuration["Authentication:Issuer"] ?? "SmartHotel.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["Authentication:Audience"] ?? "SmartHotel.Clients";

builder.Services.AddHttpClient<IBookingJwksResolver, BookingJwksResolver>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IBookingJwksResolver>((options, keyResolver) =>
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

builder.Services.AddAuthorization();

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

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartHotel Booking API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<BookingGrpcService>();
app.MapHealthChecks("/health");

// Database initialization & Startup seeding
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (context.Database.IsRelational())
        {
            await context.Database.EnsureCreatedAsync();
        }
        await DataSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not complete database initialization on startup. Ensure PostgreSQL is accessible if running full persistence.");
    }
}

app.Run();

public partial class Program { }

public interface IBookingJwksResolver
{
    Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string? kid = null);
}

public class BookingJwksResolver : IBookingJwksResolver
{
    private readonly HttpClient _httpClient;
    private readonly string _jwksUri;
    private readonly ILogger<BookingJwksResolver> _logger;
    private JsonWebKeySet? _cachedKeySet;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BookingJwksResolver(HttpClient httpClient, IConfiguration configuration, ILogger<BookingJwksResolver> logger)
    {
        _httpClient = httpClient;
        _jwksUri = configuration["Jwt:JwksUri"] ?? configuration["Authentication:JwksUri"] ?? "http://identity-service:5001/.well-known/jwks.json";
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
            if (_cachedKeySet != null && (DateTime.UtcNow - _lastFetch) < TimeSpan.FromSeconds(30))
            {
                var cachedKeys = _cachedKeySet.GetSigningKeys();
                if (string.IsNullOrEmpty(kid) || cachedKeys.Any(k => k.KeyId == kid))
                {
                    return string.IsNullOrEmpty(kid) ? cachedKeys : cachedKeys.Where(k => k.KeyId == kid);
                }
            }

            _logger.LogInformation("Fetching JWKS keys from {JwksUri}", _jwksUri);
            var response = await _httpClient.GetStringAsync(_jwksUri);
            _cachedKeySet = new JsonWebKeySet(response);
            _lastFetch = DateTime.UtcNow;

            var keys = _cachedKeySet.GetSigningKeys();
            return string.IsNullOrEmpty(kid) ? keys : keys.Where(k => k.KeyId == kid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch JWKS from {JwksUri}. Using fallback/cached keys if available.", _jwksUri);
            var fallback = _cachedKeySet?.GetSigningKeys() ?? Enumerable.Empty<SecurityKey>();
            return string.IsNullOrEmpty(kid) ? fallback : fallback.Where(k => k.KeyId == kid);
        }
        finally
        {
            _lock.Release();
        }
    }
}
