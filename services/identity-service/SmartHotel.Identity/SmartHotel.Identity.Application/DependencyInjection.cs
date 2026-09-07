using Microsoft.Extensions.DependencyInjection;
using SmartHotel.Identity.Application.Features.Auth.Services;

namespace SmartHotel.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
