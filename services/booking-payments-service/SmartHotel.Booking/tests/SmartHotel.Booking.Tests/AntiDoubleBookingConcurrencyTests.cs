using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class AntiDoubleBookingConcurrencyTests
{
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly Guid _roomTypeId = Guid.NewGuid();
    private readonly Guid _customerId1 = Guid.NewGuid();
    private readonly Guid _customerId2 = Guid.NewGuid();

    private BookingDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    private FakeHotelOpsClient CreateFakeHotelOpsClient(int capacity = 2)
    {
        return new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(
                Id: _roomTypeId,
                Name: "Deluxe Ocean View",
                PricePerNight: 20000m,
                CleaningFee: 3000m,
                AmenitiesFee: 2000m,
                Capacity: capacity,
                IsPublished: true,
                IsActive: true,
                Currency: "LKR"),
            Room = new RoomInfo(
                Id: _roomId,
                RoomTypeId: _roomTypeId,
                RoomNumber: "301",
                Floor: 3,
                Status: "Clean")
        };
    }

    [Fact]
    public async Task CreateBooking_SameRoomOverlappingDates_SecondBookingRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient();
        var handler = new CreateBookingCommandHandler(context, fakeClient);

        // Booking 1: Oct 10 to Oct 15
        var req1 = new CreateBookingRequest
        {
            CustomerId = _customerId1,
            CustomerLastName = "Fernando",
            CustomerEmail = "fernando@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 10, 10),
            CheckOutDate = new DateOnly(2026, 10, 15),
            GuestCount = 2
        };

        var res1 = await handler.Handle(new CreateBookingCommand(req1), CancellationToken.None);
        res1.Succeeded.Should().BeTrue();

        // Booking 2: Oct 12 to Oct 17 (overlaps Oct 12-15)
        var req2 = new CreateBookingRequest
        {
            CustomerId = _customerId2,
            CustomerLastName = "Perera",
            CustomerEmail = "perera@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 10, 12),
            CheckOutDate = new DateOnly(2026, 10, 17),
            GuestCount = 2
        };

        var res2 = await handler.Handle(new CreateBookingCommand(req2), CancellationToken.None);
        res2.Succeeded.Should().BeFalse();
        res2.Message.Should().Contain("already booked");
    }

    [Fact]
    public async Task CreateBooking_ContiguousDates_SameRoomCheckOutMatchesCheckIn_BothSucceed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient();
        var handler = new CreateBookingCommandHandler(context, fakeClient);

        // Booking 1: Oct 10 to Oct 15 (Guest leaves on Oct 15 morning)
        var req1 = new CreateBookingRequest
        {
            CustomerId = _customerId1,
            CustomerLastName = "Fernando",
            CustomerEmail = "fernando@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 10, 10),
            CheckOutDate = new DateOnly(2026, 10, 15),
            GuestCount = 2
        };

        var res1 = await handler.Handle(new CreateBookingCommand(req1), CancellationToken.None);
        res1.Succeeded.Should().BeTrue();

        // Booking 2: Oct 15 to Oct 20 (Guest arrives Oct 15 afternoon)
        var req2 = new CreateBookingRequest
        {
            CustomerId = _customerId2,
            CustomerLastName = "Perera",
            CustomerEmail = "perera@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 10, 15),
            CheckOutDate = new DateOnly(2026, 10, 20),
            GuestCount = 2
        };

        var res2 = await handler.Handle(new CreateBookingCommand(req2), CancellationToken.None);
        res2.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBooking_ConcurrentRequestsSameRoomAndDates_ExactlyOneSucceeds()
    {
        var dbName = Guid.NewGuid().ToString();
        var fakeClient = CreateFakeHotelOpsClient();

        // 2 concurrent tasks attempting to book the exact same room and date range
        var req1 = new CreateBookingRequest
        {
            CustomerId = _customerId1,
            CustomerLastName = "Fernando",
            CustomerEmail = "fernando@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 11, 1),
            CheckOutDate = new DateOnly(2026, 11, 5),
            GuestCount = 2
        };

        var req2 = new CreateBookingRequest
        {
            CustomerId = _customerId2,
            CustomerLastName = "Perera",
            CustomerEmail = "perera@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 11, 1),
            CheckOutDate = new DateOnly(2026, 11, 5),
            GuestCount = 2
        };

        using var context1 = CreateContext(dbName);
        using var context2 = CreateContext(dbName);

        var handler1 = new CreateBookingCommandHandler(context1, fakeClient);
        var handler2 = new CreateBookingCommandHandler(context2, fakeClient);

        var task1 = handler1.Handle(new CreateBookingCommand(req1), CancellationToken.None);
        var task2 = handler2.Handle(new CreateBookingCommand(req2), CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        successCount.Should().Be(1, "Only one booking must succeed when concurrent requests target the same room and dates");
        failureCount.Should().Be(1, "The competing concurrent booking must be rejected");
    }
}

public class FakeHotelOpsClient : IHotelOpsClient
{
    public RoomTypeInfo? RoomType { get; set; }
    public RoomInfo? Room { get; set; }
    public RoomReadinessInfo? Readiness { get; set; }
    public RoomClaimResult? ClaimResult { get; set; }
    public bool ReleaseResult { get; set; } = true;
    public int ClaimCount { get; set; }
    public int ReleaseCount { get; set; }
    public Func<Guid, Guid, string, Task<RoomClaimResult>>? CustomClaimHandler { get; set; }

    public bool AutoReady { get; set; } = false;

    public Task<RoomTypeInfo?> GetRoomTypeAsync(Guid roomTypeId, CancellationToken ct = default)
        => Task.FromResult(RoomType);

    public Task<RoomInfo?> GetRoomAsync(Guid roomId, CancellationToken ct = default)
        => Task.FromResult(Room);

    public Task<RoomReadinessInfo?> GetRoomReadinessAsync(Guid roomId, CancellationToken ct = default)
    {
        if (Readiness != null)
        {
            if (Readiness.RoomId == Guid.Empty) return Task.FromResult<RoomReadinessInfo?>(Readiness with { RoomId = roomId });
            return Task.FromResult<RoomReadinessInfo?>(Readiness);
        }
        if (AutoReady)
        {
            return Task.FromResult<RoomReadinessInfo?>(new RoomReadinessInfo(roomId, "Available", false, true, "InspectionApproved", true, null));
        }
        return Task.FromResult<RoomReadinessInfo?>(null);
    }

    public Task<RoomClaimResult> ClaimRoomAsync(Guid roomId, Guid bookingId, string source = "FrontOffice", CancellationToken ct = default)
    {
        ClaimCount++;
        if (CustomClaimHandler != null) return CustomClaimHandler(roomId, bookingId, source);
        if (ClaimResult != null) return Task.FromResult(ClaimResult);
        return Task.FromResult(new RoomClaimResult(true, true, roomId, bookingId, null, "Claimed"));
    }

    public Task<bool> ReleaseRoomClaimAsync(Guid roomId, Guid bookingId, CancellationToken ct = default)
    {
        ReleaseCount++;
        return Task.FromResult(ReleaseResult);
    }
}
