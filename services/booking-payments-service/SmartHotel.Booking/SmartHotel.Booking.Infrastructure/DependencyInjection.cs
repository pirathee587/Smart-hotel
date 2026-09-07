using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Infrastructure.Clients;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.Infrastructure.Services;

namespace SmartHotel.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<BookingDbContext>(options =>
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(BookingDbContext).Assembly.FullName)));
        }
        else
        {
            services.AddDbContext<BookingDbContext>(options =>
                options.UseInMemoryDatabase("SmartHotel_Bookings_Db"));
        }

        services.AddScoped<IBookingDbContext>(sp => sp.GetRequiredService<BookingDbContext>());

        // HTTP & gRPC clients for Hotel Ops Service
        var hotelOpsUrl = configuration["HOTEL_OPS_SERVICE_URL"] ??
                          configuration["Services:HotelOpsUrl"] ??
                          "http://hotel-ops-service:5002";

        var hotelOpsGrpcUrl = configuration["HOTEL_OPS_GRPC_URL"] ??
                              configuration["Services:HotelOpsGrpcUrl"] ??
                              "http://hotel-ops-service:5012";

        services.AddGrpcClient<SmartHotel.HotelOps.Grpc.HotelOpsGrpc.HotelOpsGrpcClient>(o =>
        {
            o.Address = new Uri(hotelOpsGrpcUrl);
        });

        services.AddHttpClient<IHotelOpsClient, HotelOpsClient>(client =>
        {
            client.BaseAddress = new Uri(hotelOpsUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // PayHere Payment Service
        services.AddSingleton<IPayHereService, PayHereService>();

        // Background Workers
        services.AddHostedService<OutboxPublisherService>();
        services.AddHostedService<ComplaintSlaEscalationWorker>();

        return services;
    }
}
