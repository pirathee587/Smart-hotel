using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Infrastructure.Clients;
using SmartHotel.HotelOps.Infrastructure.Messaging;
using SmartHotel.HotelOps.Infrastructure.Outbox;
using SmartHotel.HotelOps.Infrastructure.Persistence;
using SmartHotel.HotelOps.Infrastructure.Storage;

namespace SmartHotel.HotelOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database Context
        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                               configuration["HOTELOPS_DB_CONNECTION_STRING"] ??
                               "Host=postgres;Port=5432;Database=smarthotel_hotelops_db;Username=postgres;Password=supersecret_postgres_password;";

        var provider = configuration["DatabaseProvider"] ?? "PostgreSQL";

        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<HotelOpsDbContext>(options =>
                options.UseInMemoryDatabase("SmartHotel_HotelOps_Dev"));
        }
        else
        {
            services.AddDbContext<HotelOpsDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        services.AddScoped<IHotelOpsDbContext>(sp => sp.GetRequiredService<HotelOpsDbContext>());

        // Storage Service (Supabase / Local fallback)
        services.AddHttpClient<IImageStorageService, SupabaseStorageService>();

        // Identity Service Client (Scaffolded Manager validation)
        services.AddScoped<IIdentityServiceClient, IdentityServiceClient>();

        // Booking Availability Client (HTTP calls to booking-payments-service)
        services.AddHttpClient<IBookingAvailabilityClient, BookingAvailabilityClient>();

        // Background Services (Outbox Publisher & RabbitMQ Checkout Consumer)
        services.AddHostedService<OutboxPublisherService>();
        services.AddHostedService<BookingCheckedOutConsumerService>();

        return services;
    }
}
