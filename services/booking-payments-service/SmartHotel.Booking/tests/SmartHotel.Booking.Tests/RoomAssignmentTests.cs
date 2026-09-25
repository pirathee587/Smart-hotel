using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class RoomAssignmentTests
{
    private BookingDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task AssignRoom_ValidTargetRoom_SucceedsAndAudits()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var roomTypeId = Guid.NewGuid();
        var oldRoomId = Guid.NewGuid();
        var newRoomId = Guid.NewGuid();

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-ASSIGN-01",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Gunaratne",
            RoomTypeId = roomTypeId,
            RoomId = oldRoomId,
            RoomNumber = "101",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Room = new RoomInfo(newRoomId, roomTypeId, "102", 1, "Available"),
            Readiness = new RoomReadinessInfo(newRoomId, "Available", false, true, "InspectionApproved", true, null)
        };

        var handler = new AssignRoomCommandHandler(context, fakeOps);
        var command = new AssignRoomCommand(booking.Id, newRoomId, Guid.NewGuid(), "Guest requested quiet corner room");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.RoomId.Should().Be(newRoomId);
        result.Data.RoomNumber.Should().Be("102");

        // Verify database updated
        var updated = await context.Bookings.FindAsync(booking.Id);
        updated!.RoomId.Should().Be(newRoomId);
        updated.RoomNumber.Should().Be("102");

        // Verify audit log
        var audit = await context.FrontOfficeAuditLogs.FirstOrDefaultAsync(l => l.BookingId == booking.Id && l.Action == "RoomAssigned");
        audit.Should().NotBeNull();
        audit!.Details.Should().Contain("101");
        audit.Details.Should().Contain("102");
        audit.Details.Should().Contain("Guest requested quiet corner room");

        // Verify outbox message
        var outbox = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "frontoffice.room-assigned");
        outbox.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignRoom_DateOverlapConflict_RejectsAssignment()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var roomTypeId = Guid.NewGuid();
        var targetRoomId = Guid.NewGuid();

        // Existing reservation already on the target room
        var existingBooking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-EXISTING-01",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Fernando",
            RoomTypeId = roomTypeId,
            RoomId = targetRoomId,
            RoomNumber = "201",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(existingBooking);

        // New booking wanting to switch to target room for overlapping dates
        var newBooking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-OVERLAP-01",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Dias",
            RoomTypeId = roomTypeId,
            RoomId = Guid.NewGuid(),
            RoomNumber = "202",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)), // Overlaps days 2-4!
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(newBooking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Room = new RoomInfo(targetRoomId, roomTypeId, "201", 2, "Available"),
            Readiness = new RoomReadinessInfo(targetRoomId, "Available", false, true, "InspectionApproved", true, null)
        };

        var handler = new AssignRoomCommandHandler(context, fakeOps);
        var result = await handler.Handle(new AssignRoomCommand(newBooking.Id, targetRoomId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("already reserved");

        // Target room was NOT assigned
        var unassigned = await context.Bookings.FindAsync(newBooking.Id);
        unassigned!.RoomNumber.Should().Be("202");
    }

    [Fact]
    public async Task AssignRoom_MismatchedRoomType_RejectsAssignment()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var deluxeTypeId = Guid.NewGuid();
        var suiteTypeId = Guid.NewGuid();
        var suiteRoomId = Guid.NewGuid();

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-DELUXE-01",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Dias",
            RoomTypeId = deluxeTypeId,
            RoomId = Guid.NewGuid(),
            RoomNumber = "101",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            // Room belongs to Suite, but booking is for Deluxe
            Room = new RoomInfo(suiteRoomId, suiteTypeId, "501", 5, "Available")
        };

        var handler = new AssignRoomCommandHandler(context, fakeOps);
        var result = await handler.Handle(new AssignRoomCommand(booking.Id, suiteRoomId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Mismatched room type assignment is not permitted");
    }

    [Fact]
    public async Task AssignRoom_MaintenanceRestrictedRoom_RejectsAssignment()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var roomTypeId = Guid.NewGuid();
        var targetRoomId = Guid.NewGuid();

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-MAINT-01",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Dias",
            RoomTypeId = roomTypeId,
            RoomId = Guid.NewGuid(),
            RoomNumber = "101",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Room = new RoomInfo(targetRoomId, roomTypeId, "102", 1, "Available"),
            Readiness = new RoomReadinessInfo(targetRoomId, "Available", true, true, "MaintenanceRestricted", false, "AC compressor broken")
        };

        var handler = new AssignRoomCommandHandler(context, fakeOps);
        var result = await handler.Handle(new AssignRoomCommand(booking.Id, targetRoomId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("active Maintenance restriction");
    }

    [Fact]
    public async Task AssignRoom_AlreadyCheckedInBooking_RejectsAssignment()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var roomTypeId = Guid.NewGuid();
        var targetRoomId = Guid.NewGuid();

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-CHECKEDIN-01",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Dias",
            RoomTypeId = roomTypeId,
            RoomId = Guid.NewGuid(),
            RoomNumber = "101",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = BookingStatus.CheckedIn
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Room = new RoomInfo(targetRoomId, roomTypeId, "102", 1, "Available"),
            Readiness = new RoomReadinessInfo(targetRoomId, "Available", false, true, "InspectionApproved", true, null)
        };

        var handler = new AssignRoomCommandHandler(context, fakeOps);
        var result = await handler.Handle(new AssignRoomCommand(booking.Id, targetRoomId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Only Pending or Confirmed reservations can be assigned");
    }
}
