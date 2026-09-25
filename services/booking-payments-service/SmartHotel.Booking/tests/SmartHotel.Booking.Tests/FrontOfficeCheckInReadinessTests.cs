using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Features.Kiosk.Commands;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class FrontOfficeCheckInReadinessTests
{
    private BookingDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    private static Domain.Entities.Booking CreateConfirmedBooking(Guid? roomId = null)
    {
        return new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = $"TH-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Silva",
            CustomerEmail = "silva@example.com",
            RoomId = roomId ?? Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = BookingStatus.Confirmed,
            TotalAmount = 25000m
        };
    }

    [Fact]
    public async Task CheckIn_ReadyRoom_Succeeds_ClaimsAtomically_AndWritesAuditLog()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "Available", false, true, "InspectionApproved", true, null),
            ClaimResult = new RoomClaimResult(true, true, booking.RoomId, booking.Id, null, "Claimed")
        };

        var handler = new CheckInCommandHandler(context, fakeOps);
        var actorId = Guid.NewGuid();
        var command = new CheckInCommand(booking.Id, actorId, "FrontOffice");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Status.Should().Be(BookingStatus.CheckedIn);
        fakeOps.ClaimCount.Should().Be(1);

        // Verify Booking DB status
        var updated = await context.Bookings.FindAsync(booking.Id);
        updated!.Status.Should().Be(BookingStatus.CheckedIn);

        // Verify outbox message emitted
        var outbox = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "booking.checkedin");
        outbox.Should().NotBeNull();
        outbox!.Content.Should().Contain(booking.Id.ToString());

        // Verify immutable audit log written
        var audit = await context.FrontOfficeAuditLogs.FirstOrDefaultAsync(l => l.BookingId == booking.Id && l.Action == "CheckIn");
        audit.Should().NotBeNull();
        audit!.ActorRole.Should().Be("Receptionist");
        audit.Details.Should().Contain("checked into room");
    }

    [Fact]
    public async Task CheckIn_DirtyRoom_FailsClosed_WithReadinessBlocker()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "Dirty", false, false, "Dirty", false, "Room is dirty and has not been cleaned"),
            ClaimResult = new RoomClaimResult(false, false, booking.RoomId, null, "Room is dirty and has not been cleaned", "Room is dirty")
        };

        var handler = new CheckInCommandHandler(context, fakeOps);
        var command = new CheckInCommand(booking.Id, Guid.NewGuid(), "FrontOffice");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Room is dirty");

        // Status must NOT have changed
        var unchanged = await context.Bookings.FindAsync(booking.Id);
        unchanged!.Status.Should().Be(BookingStatus.Confirmed);
        context.FrontOfficeAuditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckIn_InCleaning_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "InCleaning", false, false, "CleaningInProgress", false, "Room is currently being cleaned"),
            ClaimResult = new RoomClaimResult(false, false, booking.RoomId, null, "Room is currently being cleaned", "Room is cleaning")
        };

        var handler = new CheckInCommandHandler(context, fakeOps);
        var result = await handler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Room is currently being cleaned");
    }

    [Fact]
    public async Task CheckIn_OutOfOrder_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "OutOfOrder", true, false, "OutOfOrder", false, "Room is OutOfOrder"),
            ClaimResult = new RoomClaimResult(false, false, booking.RoomId, null, "Room is OutOfOrder", "Out of order")
        };

        var handler = new CheckInCommandHandler(context, fakeOps);
        var result = await handler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("OutOfOrder");
    }

    [Fact]
    public async Task CheckIn_ActiveMaintenanceRestriction_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "Available", true, true, "MaintenanceRestricted", false, "Room has active maintenance restriction: Plumbing leak"),
            ClaimResult = new RoomClaimResult(false, false, booking.RoomId, null, "Room has active maintenance restriction: Plumbing leak", "Maintenance restricted")
        };

        var handler = new CheckInCommandHandler(context, fakeOps);
        var result = await handler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("active maintenance restriction");
    }

    [Fact]
    public async Task CheckIn_MissingHousekeepingInspectionApproval_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "Available", false, false, "CleaningCompletedUnapproved", false, "Room cleaned but pending supervisor inspection"),
            ClaimResult = new RoomClaimResult(false, false, booking.RoomId, null, "Room cleaned but pending supervisor inspection", "Unapproved")
        };

        var handler = new CheckInCommandHandler(context, fakeOps);
        var result = await handler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("pending supervisor inspection");
    }

    [Fact]
    public async Task CheckIn_DuplicateCallOnAlreadyCheckedInBooking_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = CreateConfirmedBooking();
        booking.Status = BookingStatus.CheckedIn;
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient();
        var handler = new CheckInCommandHandler(context, fakeOps);

        var result = await handler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Status.Should().Be(BookingStatus.CheckedIn);
        // Hotel Ops claim should not have been called again
        fakeOps.ClaimCount.Should().Be(0);
        // No duplicate outbox messages
        (await context.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CheckIn_ConflictingOccupancy_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var roomId = Guid.NewGuid();

        // Existing checked-in reservation in this room
        var activeOccupant = CreateConfirmedBooking(roomId);
        activeOccupant.Status = BookingStatus.CheckedIn;
        context.Bookings.Add(activeOccupant);

        // New reservation attempting check-in to same room
        var newBooking = CreateConfirmedBooking(roomId);
        context.Bookings.Add(newBooking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(roomId, "Occupied", false, true, "Occupied", false, "Room is currently occupied"),
            ClaimResult = new RoomClaimResult(false, false, roomId, activeOccupant.Id, "Room is currently occupied", "Occupied")
        };

        var handler = new CheckInCommandHandler(context, fakeOps);
        var result = await handler.Handle(new CheckInCommand(newBooking.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("occupancy");
    }

    [Fact]
    public async Task KioskAndStaffCheckIn_ShareIdenticalReadinessAndAtomicClaimGate()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "Dirty", false, false, "Dirty", false, "Room is dirty"),
            ClaimResult = new RoomClaimResult(false, false, booking.RoomId, null, "Room is dirty", "Room is dirty")
        };

        var kioskHandler = new KioskCheckInCommandHandler(context, fakeOps);
        var kioskResult = await kioskHandler.Handle(new KioskCheckInCommand(booking.BookingReference, booking.CustomerLastName), CancellationToken.None);

        kioskResult.Succeeded.Should().BeFalse();
        kioskResult.Message.Should().Contain("Room is dirty");

        // Staff check-in also rejected for the exact same reason
        var staffHandler = new CheckInCommandHandler(context, fakeOps);
        var staffResult = await staffHandler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        staffResult.Succeeded.Should().BeFalse();
        staffResult.Message.Should().Contain("Room is dirty");
    }
}
