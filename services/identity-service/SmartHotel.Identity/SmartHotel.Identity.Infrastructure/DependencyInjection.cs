using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Infrastructure.Persistence;
using SmartHotel.Identity.Infrastructure.Services;

namespace SmartHotel.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"]?.ToLowerInvariant() ?? "postgresql";

        if (provider == "inmemory")
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("SmartHotelIdentityDb");
            });
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["IDENTITY_DB_CONNECTION_STRING"]
                ?? "Host=localhost;Port=5432;Database=smarthotel_identity_db;Username=identity_user;Password=identity_secure_password_123;";

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });
        }

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<RsaKeyManager>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddSingleton<IRateLimiterService, InMemoryRateLimiterService>();

        return services;
    }
}
