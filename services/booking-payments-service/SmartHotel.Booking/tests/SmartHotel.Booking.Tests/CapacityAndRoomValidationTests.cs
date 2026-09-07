using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class CapacityAndRoomValidationTests
{
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly Guid _roomTypeId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    private BookingDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task CreateBooking_GuestCountExceedsCapacity_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var fakeClient = new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(_roomTypeId, "Single Standard", 12000m, 1500m, 1000m, Capacity: 1, IsPublished: true, IsActive: true),
            Room = new RoomInfo(_roomId, _roomTypeId, "101", 1, "Clean")
        };

        var handler = new CreateBookingCommandHandler(context, fakeClient);

        var request = new CreateBookingRequest
        {
            CustomerId = _customerId,
            CustomerLastName = "Gunawardena",
            CustomerEmail = "guna@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 11, 10),
            CheckOutDate = new DateOnly(2026, 11, 12),
            GuestCount = 3 // Exceeds capacity of 1
        };

        var result = await handler.Handle(new CreateBookingCommand(request), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("exceeds maximum room capacity");
    }

    [Fact]
    public async Task CreateBooking_UnpublishedRoomType_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var fakeClient = new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(_roomTypeId, "Executive Penthouse", 85000m, 5000m, 5000m, Capacity: 4, IsPublished: false, IsActive: true),
            Room = new RoomInfo(_roomId, _roomTypeId, "501", 5, "Clean")
        };

        var handler = new CreateBookingCommandHandler(context, fakeClient);

        var request = new CreateBookingRequest
        {
            CustomerId = _customerId,
            CustomerLastName = "Gunawardena",
            CustomerEmail = "guna@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 11, 10),
            CheckOutDate = new DateOnly(2026, 11, 12),
            GuestCount = 2
        };

        var result = await handler.Handle(new CreateBookingCommand(request), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("not available for public booking");
    }

    [Fact]
    public async Task CreateBooking_NonExistentRoom_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var fakeClient = new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(_roomTypeId, "Deluxe Double", 25000m, 2000m, 1500m, Capacity: 2, IsPublished: true, IsActive: true),
            Room = null // Room does not exist in Hotel Ops
        };

        var handler = new CreateBookingCommandHandler(context, fakeClient);

        var request = new CreateBookingRequest
        {
            CustomerId = _customerId,
            CustomerLastName = "Gunawardena",
            CustomerEmail = "guna@example.com",
            RoomId = _roomId,
            RoomTypeId = _roomTypeId,
            CheckInDate = new DateOnly(2026, 11, 10),
            CheckOutDate = new DateOnly(2026, 11, 12),
            GuestCount = 2
        };

        var result = await handler.Handle(new CreateBookingCommand(request), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Selected room does not exist");
    }
}
