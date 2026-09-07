using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartHotel.Identity.API.Middleware;
using SmartHotel.Identity.Application;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Infrastructure;
using SmartHotel.Identity.Infrastructure.Persistence;
using SmartHotel.Identity.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

// 2. Swagger with Bearer token authentication
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartHotel Identity Service API",
        Version = "v1",
        Description = "Authentication and Identity Service for SmartHotel Backend (RS256 JWT, TPT Persistence)"
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

// 4. RS256 JWT Authentication configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<RsaKeyManager, IConfiguration>((options, keyManager, configuration) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "SmartHotel.Identity",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "SmartHotel.Clients",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keyManager.GetSecurityKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartHotel Identity API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();

app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Database initialization & Startup seeding
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (context.Database.IsRelational())
        {
            await context.Database.EnsureCreatedAsync();
        }
        await DataSeeder.SeedAsync(context, hasher, logger);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not complete database initialization on startup. Ensure PostgreSQL is accessible if running full persistence.");
    }
}

app.Run();

public partial class Program { }
