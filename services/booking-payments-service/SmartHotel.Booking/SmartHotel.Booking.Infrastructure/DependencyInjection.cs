using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Infrastructure.Clients;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.Infrastructure.Services;

namespace SmartHotel.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration,
        IHostEnvironment? environment = null)
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

        // PayHere Payment Service & Payment Gateway
        services.AddSingleton<IPayHereService, PayHereService>();

        var environmentName = environment?.EnvironmentName
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        var stubEnvironmentAllowed = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase);

        var useStubGateway = configuration.GetValue<bool>("Payment:UseStubGateway", false)
            || configuration.GetValue<bool>("PAYMENT_USE_STUB_GATEWAY", false);

        if (useStubGateway && !stubEnvironmentAllowed)
        {
            throw new InvalidOperationException(
                $"CRITICAL: StubPaymentGateway cannot be used in the '{environmentName}' environment. " +
                "It is allowed only in Development, Testing, or Test.");
        }

        if (useStubGateway)
        {
            services.AddScoped<IPaymentGateway, StubPaymentGateway>();
        }
        else
        {
            services.AddScoped<IPaymentGateway, ProductionPaymentGateway>();
        }
        services.AddScoped<IEscrowPaymentProvider, UnsupportedEscrowPaymentProvider>();

        // Background Workers
        services.AddHostedService<OutboxPublisherService>();
        services.AddHostedService<ComplaintSlaEscalationWorker>();

        return services;
    }
}
