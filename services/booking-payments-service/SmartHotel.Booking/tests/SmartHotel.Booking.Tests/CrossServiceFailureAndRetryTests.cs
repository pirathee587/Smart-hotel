using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class FailingCommitBookingDbContext : BookingDbContext
{
    public FailingCommitBookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new DbUpdateConcurrencyException("Simulated commit collision");
    }
}

public class CrossServiceFailureAndRetryTests
{
    private DbContextOptions<BookingDbContext> CreateOptions(string dbName)
    {
        return new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
    }

    private static Domain.Entities.Booking CreateConfirmedBooking(Guid? roomId = null)
    {
        return new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = $"TH-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Perera",
            CustomerEmail = "perera@example.com",
            RoomId = roomId ?? Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = BookingStatus.Confirmed,
            TotalAmount = 30000m
        };
    }

    [Fact]
    public async Task CrossServiceFailure_HotelOpsUnavailable_FailsClosed_PreservesBookingState()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateOptions(dbName);
        using var context = new BookingDbContext(options);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var failingOps = new FakeHotelOpsClient
        {
            Readiness = null // Unavailable
        };

        var handler = new CheckInCommandHandler(context, failingOps);
        var result = await handler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Hotel Ops readiness is unavailable");

        // Verify booking state was NOT changed
        var preserved = await context.Bookings.FindAsync(booking.Id);
        preserved!.Status.Should().Be(BookingStatus.Confirmed);

        // No outbox messages emitted
        (await context.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CrossServiceConcurrency_RoomClaimConflict_RejectsCheckIn()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateOptions(dbName);
        using var context = new BookingDbContext(options);

        var booking = CreateConfirmedBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var conflictOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "Available", false, true, "InspectionApproved", true, null),
            ClaimResult = new RoomClaimResult(false, false, booking.RoomId, null, "Room is locked or being claimed by concurrent request.", "Conflict")
        };

        var handler = new CheckInCommandHandler(context, conflictOps);
        var result = await handler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("concurrent request");

        // Verify booking state remained Confirmed
        var preserved = await context.Bookings.FindAsync(booking.Id);
        preserved!.Status.Should().Be(BookingStatus.Confirmed);
        (await context.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompensatingTransaction_ReleaseClaimInvoked_WhenCommitFails()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateOptions(dbName);

        var booking = CreateConfirmedBooking();
        using (var seedContext = new BookingDbContext(options))
        {
            seedContext.Bookings.Add(booking);
            await seedContext.SaveChangesAsync();
        }

        var fakeOps = new FakeHotelOpsClient
        {
            Readiness = new RoomReadinessInfo(booking.RoomId, "Available", false, true, "InspectionApproved", true, null),
            ClaimResult = new RoomClaimResult(true, true, booking.RoomId, booking.Id, null, "Claimed")
        };

        using var failingContext = new FailingCommitBookingDbContext(options);
        var handler = new CheckInCommandHandler(failingContext, fakeOps);

        var result = await handler.Handle(new CheckInCommand(booking.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Check-in failed during commit");

        // Verify Hotel Ops claim was invoked first
        fakeOps.ClaimCount.Should().Be(1);

        // Verify compensating release was called on Hotel Ops
        fakeOps.ReleaseCount.Should().Be(1);
    }
}
