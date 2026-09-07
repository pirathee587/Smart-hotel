using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Features.Rooms.Commands;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.HotelOps.Tests;

public class OutboxMessageTests
{
    private HotelOpsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HotelOpsDbContext(options);
    }

    [Fact]
    public async Task UpdateRoomStatus_ShouldWriteOutboxMessage_WithStatusChangedPayload()
    {
        using var context = CreateDbContext();
        var roomId = Guid.NewGuid();
        var hotelId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var roomType = new RoomType
        {
            Id = roomTypeId,
            HotelId = hotelId,
            Name = "Executive Suite",
            PricePerNight = 120m,
            IsPublished = true
        };
        var room = new Room
        {
            Id = roomId,
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            RoomNumber = "201",
            Floor = 2,
            Status = RoomStatus.Available
        };

        context.RoomTypes.Add(roomType);
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        var handler = new UpdateRoomStatusCommandHandler(context);

        // Act: Update status from Available to Occupied
        var command = new UpdateRoomStatusCommand(roomId, RoomStatus.Occupied);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Command succeeded
        result.Succeeded.Should().BeTrue();
        result.Data!.Status.Should().Be(RoomStatus.Occupied);

        // Assert: OutboxMessage was written
        var outboxMessages = await context.OutboxMessages.ToListAsync();
        outboxMessages.Should().HaveCount(1);

        var message = outboxMessages.First();
        message.Type.Should().Be("room.status_changed");
        message.ProcessedOnUtc.Should().BeNull();

        // Deserialize content to verify payload details
        using var doc = JsonDocument.Parse(message.Content);
        var root = doc.RootElement;
        root.GetProperty("RoomId").GetString().Should().Be(roomId.ToString());
        root.GetProperty("RoomNumber").GetString().Should().Be("201");
        root.GetProperty("PreviousStatus").GetString().Should().Be("Available");
        root.GetProperty("NewStatus").GetString().Should().Be("Occupied");
    }

    [Fact]
    public async Task UpdateRoomStatus_ToOutOfOrder_ShouldIncludeReasonInOutboxPayload()
    {
        using var context = CreateDbContext();
        var roomId = Guid.NewGuid();
        var hotelId = Guid.NewGuid();

        var roomType = new RoomType
        {
            Id = Guid.NewGuid(),
            HotelId = hotelId,
            Name = "Deluxe Room",
            PricePerNight = 100m,
            IsPublished = true
        };
        var room = new Room
        {
            Id = roomId,
            HotelId = hotelId,
            RoomTypeId = roomType.Id,
            RoomNumber = "305",
            Floor = 3,
            Status = RoomStatus.Occupied
        };

        context.RoomTypes.Add(roomType);
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        var handler = new UpdateRoomStatusCommandHandler(context);

        // Act: Move to OutOfOrder with reason
        var command = new UpdateRoomStatusCommand(roomId, RoomStatus.OutOfOrder, "Water leakage from ceiling");
        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue(result.Message);

        var outboxMessage = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "room.status_changed");
        outboxMessage.Should().NotBeNull();

        using var doc = JsonDocument.Parse(outboxMessage!.Content);
        var root = doc.RootElement;
        root.GetProperty("PreviousStatus").GetString().Should().Be("Occupied");
        root.GetProperty("NewStatus").GetString().Should().Be("OutOfOrder");
        root.GetProperty("Reason").GetString().Should().Be("Water leakage from ceiling");
    }
}
