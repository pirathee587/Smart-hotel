using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SmartHotel.Booking.API.Controllers;
using SmartHotel.Booking.Application.Features.Kiosk.DTOs;
using SmartHotel.Booking.Application.Features.Reviews.DTOs;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class BookingApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PAYHERE_MERCHANT_ID"] = "test-merchant",
            ["PAYHERE_MERCHANT_SECRET"] = "test-secret-not-production",
            ["PAYHERE_IS_SANDBOX"] = "true"
        }));
        builder.ConfigureServices(services =>
        {
            // Remove real DbContext registration and use dedicated InMemory database for integration test
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<BookingDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BookingDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            var hotelOpsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(Application.Interfaces.IHotelOpsClient));
            if (hotelOpsDescriptor != null)
            {
                services.Remove(hotelOpsDescriptor);
            }
            services.AddScoped<Application.Interfaces.IHotelOpsClient>(_ => new FakeHotelOpsClient { AutoReady = true });
        });
    }
}

public class BookingControllerIntegrationTests : IClassFixture<BookingApiFactory>
{
    private readonly HttpClient _client;
    private readonly BookingApiFactory _factory;

    public BookingControllerIntegrationTests(BookingApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task KioskLookup_And_CheckIn_Flow_ViaHttp_Succeeds()
    {
        // Seed a test booking in the test DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var booking = new Domain.Entities.Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = "TH-2026-INT001",
                CustomerId = Guid.NewGuid(),
                CustomerLastName = "Dassanayake",
                CustomerEmail = "dassa@example.com",
                RoomId = Guid.NewGuid(),
                RoomTypeId = Guid.NewGuid(),
                CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                TotalAmount = 35000m,
                Status = BookingStatus.Confirmed
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
        }

        // 1. Kiosk lookup
        var lookupReq = new KioskLookupRequest("TH-2026-INT001", "Dassanayake");
        var lookupResp = await _client.PostAsJsonAsync("/api/v1/kiosk/lookup", lookupReq);

        lookupResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var kioskDto = await lookupResp.Content.ReadFromJsonAsync<KioskBookingDto>();
        kioskDto.Should().NotBeNull();
        kioskDto!.BookingReference.Should().Be("TH-2026-INT001");
        kioskDto.CanCheckIn.Should().BeTrue();

        // 2. Kiosk check-in
        var checkInReq = new KioskActionRequest("TH-2026-INT001", "Dassanayake");
        var checkInResp = await _client.PostAsJsonAsync("/api/v1/kiosk/check-in", checkInReq);

        checkInResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkedInDto = await checkInResp.Content.ReadFromJsonAsync<KioskBookingDto>();
        checkedInDto.Should().NotBeNull();
        checkedInDto!.Status.Should().Be(BookingStatus.CheckedIn);
        checkedInDto.CanCheckOut.Should().BeTrue();
    }

    [Fact]
    public async Task PayHereWebhook_ValidSignature_ConfirmsBookingViaHttp()
    {
        string bookingRef = "TH-2026-PAY001";
        decimal totalAmount = 50000.00m;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var booking = new Domain.Entities.Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = bookingRef,
                CustomerId = Guid.NewGuid(),
                CustomerLastName = "Senanayake",
                CustomerEmail = "sena@example.com",
                RoomId = Guid.NewGuid(),
                RoomTypeId = Guid.NewGuid(),
                CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                TotalAmount = totalAmount,
                Status = BookingStatus.PendingPayment,
                PayHereOrderId = bookingRef
            };
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = totalAmount,
                Currency = "LKR",
                Provider = PaymentProvider.PayHere,
                PayHereOrderId = bookingRef,
                Status = PaymentStatus.Created
            };
            db.Bookings.Add(booking);
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
        }

        // Prepare PayHere webhook payload with valid signature
        var config = _factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var merchantId = config["PAYHERE_MERCHANT_ID"]!;
        var merchantSecret = config["PAYHERE_MERCHANT_SECRET"]!;
        const string amount = "50000.00";
        const string currency = "LKR";
        const int statusCode = 2; // Success

        var secretHash = PayHereService.ComputeMd5(merchantSecret);
        var rawSig = $"{merchantId}{bookingRef}{amount}{currency}{statusCode}{secretHash}";
        var md5Sig = PayHereService.ComputeMd5(rawSig);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["merchant_id"] = merchantId,
            ["order_id"] = bookingRef,
            ["payment_id"] = "PH_PAY_777888",
            ["payhere_amount"] = amount,
            ["payhere_currency"] = currency,
            ["status_code"] = statusCode.ToString(),
            ["md5sig"] = md5Sig,
            ["status_message"] = "Successfully completed."
        });

        var webhookResp = await _client.PostAsync("/api/v1/payments/payhere/notify", formContent);

        webhookResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify booking updated in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var updatedBooking = await db.Bookings.FirstOrDefaultAsync(b => b.BookingReference == bookingRef);
            updatedBooking.Should().NotBeNull();
            updatedBooking!.Status.Should().Be(BookingStatus.Confirmed);

            var updatedPayment = await db.Payments.FirstOrDefaultAsync(p => p.PayHereOrderId == bookingRef);
            updatedPayment.Should().NotBeNull();
            updatedPayment!.Status.Should().Be(PaymentStatus.Completed);
            updatedPayment!.PayHerePaymentId.Should().Be("PH_PAY_777888");
        }
    }

    [Fact]
    public async Task GetReviewsByRoomType_PublicEndpoint_ReturnsOnlyPublishedReviews()
    {
        var roomTypeId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var review1 = new Review
            {
                Id = Guid.NewGuid(),
                BookingId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                RoomTypeId = roomTypeId,
                Rating = 5,
                Comment = "Brilliant suite view!",
                IsPublished = true
            };
            var review2 = new Review
            {
                Id = Guid.NewGuid(),
                BookingId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                RoomTypeId = roomTypeId,
                Rating = 1,
                Comment = "Spam review hidden by moderator",
                IsPublished = false // Hidden
            };
            db.Reviews.AddRange(review1, review2);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/reviews/room-type/{roomTypeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviews = await response.Content.ReadFromJsonAsync<List<ReviewDto>>();
        reviews.Should().NotBeNull();
        reviews!.Should().HaveCount(1);
        reviews![0].Comment.Should().Be("Brilliant suite view!");
    }
}
