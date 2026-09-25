using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.HotelOps.Application.Features.Rooms.Queries;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Infrastructure.Clients;
using SmartHotel.HotelOps.Infrastructure.Persistence;

namespace SmartHotel.HotelOps.Tests;

public class AvailabilityTests
{
    [Fact]
    public async Task BookingClient_PreservesLocalInventorySentinel()
    {
        using var httpClient = new HttpClient(new StubHandler(
            """{"roomTypeId":"33333333-3333-3333-3333-333333333331","totalRooms":-1,"availableCount":-1,"firstAvailableRoomId":null}"""));
        var client = new BookingAvailabilityClient(
            httpClient,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<BookingAvailabilityClient>.Instance);

        var result = await client.GetAvailabilityAsync(
            Guid.Parse("33333333-3333-3333-3333-333333333331"),
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        result.TotalRooms.Should().Be(-1);
        result.AvailableCount.Should().Be(-1);
        result.FirstAvailableRoomId.Should().BeNull();
    }

    [Theory]
    [InlineData(RoomStatus.Available, 1)]
    [InlineData(RoomStatus.Occupied, 0)]
    [InlineData(RoomStatus.Dirty, 0)]
    [InlineData(RoomStatus.InCleaning, 0)]
    [InlineData(RoomStatus.Inspected, 0)]
    [InlineData(RoomStatus.OutOfOrder, 0)]
    public async Task Availability_OnlyAvailableRoomsAreBookable(RoomStatus status, int expectedCount)
    {
        await using var db = CreateContext();
        var roomType = CreateRoomType(isPublished: true);
        var room = CreateRoom(roomType, status);
        db.AddRange(roomType, room);
        await db.SaveChangesAsync();

        var handler = new GetAvailabilityQueryHandler(db, new SentinelAvailabilityClient());
        var result = await handler.Handle(
            new GetAvailabilityQuery(DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().ContainSingle();
        result.Data![0].AvailableCount.Should().Be(expectedCount);
        result.Data[0].FirstAvailableRoomId.Should().Be(expectedCount == 1 ? room.Id : null);
    }

    [Fact]
    public async Task Availability_UnpublishedRoomTypeIsExcluded()
    {
        await using var db = CreateContext();
        var roomType = CreateRoomType(isPublished: false);
        db.AddRange(roomType, CreateRoom(roomType, RoomStatus.Available));
        await db.SaveChangesAsync();

        var handler = new GetAvailabilityQueryHandler(db, new SentinelAvailabilityClient());
        var result = await handler.Handle(
            new GetAvailabilityQuery(DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    private static HotelOpsDbContext CreateContext() => new(
        new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RoomType CreateRoomType(bool isPublished) => new()
    {
        Id = Guid.NewGuid(),
        HotelId = Guid.NewGuid(),
        Name = "Test Room Type",
        Capacity = 2,
        PricePerNight = 100,
        IsPublished = isPublished,
        IsActive = true
    };

    private static Room CreateRoom(RoomType roomType, RoomStatus status) => new()
    {
        Id = Guid.NewGuid(),
        HotelId = roomType.HotelId,
        RoomTypeId = roomType.Id,
        RoomNumber = Guid.NewGuid().ToString("N")[..8],
        Floor = 1,
        Status = status
    };

    private sealed class SentinelAvailabilityClient : IBookingAvailabilityClient
    {
        public Task<RoomAvailabilityResult> GetAvailabilityAsync(
            Guid roomTypeId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default) =>
            Task.FromResult(new RoomAvailabilityResult(roomTypeId, -1, -1, null));
    }

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
    }
}
