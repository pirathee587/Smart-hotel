using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Features.Kiosk.Commands;
using SmartHotel.Booking.Application.Features.Kiosk.Queries;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class KioskSelfServiceTests
{
    private BookingDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task KioskLookup_CaseInsensitiveReferenceAndLastName_ReturnsBooking()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-KIOSK1",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Wickramasinghe",
            CustomerEmail = "wick@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var handler = new KioskLookupBookingQueryHandler(context);
        // Search with lowercase and extra whitespace
        var query = new KioskLookupBookingQuery("  th-2026-kiosk1  ", "  wickramasinghe  ");

        var result = await handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.BookingReference.Should().Be("TH-2026-KIOSK1");
        result.Data.CanCheckIn.Should().BeTrue();
        result.Data.CanCheckOut.Should().BeFalse();
    }

    [Fact]
    public async Task KioskLookup_WrongLastName_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-KIOSK2",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Wickramasinghe",
            CustomerEmail = "wick@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var handler = new KioskLookupBookingQueryHandler(context);
        var query = new KioskLookupBookingQuery("TH-2026-KIOSK2", "Smith");

        var result = await handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("No reservation found");
    }

    [Fact]
    public async Task KioskCheckIn_SameDayConfirmedBooking_SucceedsAndEmitsOutboxMessage()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-KIOSK3",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Wickramasinghe",
            CustomerEmail = "wick@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var handler = new KioskCheckInCommandHandler(context,Ready(booking.RoomId));
        var command = new KioskCheckInCommand("TH-2026-KIOSK3", "Wickramasinghe");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Status.Should().Be(BookingStatus.CheckedIn);
        result.Data.CanCheckIn.Should().BeFalse();
        result.Data.CanCheckOut.Should().BeTrue();

        var updated = await context.Bookings.FindAsync(booking.Id);
        updated!.Status.Should().Be(BookingStatus.CheckedIn);

        var outbox = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "booking.checkedin");
        outbox.Should().NotBeNull();
        outbox!.Content.Should().Contain("Kiosk");
    }

    [Fact]
    public async Task KioskCheckIn_EarlyCheckInAttempt_RejectedWithGuidance()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-KIOSK4",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Wickramasinghe",
            CustomerEmail = "wick@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), // Tomorrow!
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var handler = new KioskCheckInCommandHandler(context,Ready(booking.RoomId));
        var command = new KioskCheckInCommand("TH-2026-KIOSK4", "Wickramasinghe");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Early check-in not permitted at kiosk");
        result.Message.Should().Contain("front desk");
    }

    [Fact]
    public async Task KioskCheckOut_CheckedInBooking_SucceedsAndEmitsCheckedOutOutboxMessage()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-KIOSK5",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Wickramasinghe",
            CustomerEmail = "wick@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = BookingStatus.CheckedIn
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var handler = new KioskCheckOutCommandHandler(context);
        var command = new KioskCheckOutCommand("TH-2026-KIOSK5", "Wickramasinghe");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Status.Should().Be(BookingStatus.CheckedOut);

        var updated = await context.Bookings.FindAsync(booking.Id);
        updated!.Status.Should().Be(BookingStatus.CheckedOut);

        // This event triggers Hotel Ops Service to mark room as Dirty
        var outbox = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "booking.checkedout");
        outbox.Should().NotBeNull();
        outbox!.Content.Should().Contain("Kiosk");
    }

    private static FakeHotelOpsClient Ready(Guid roomId)=>new(){Readiness=new(roomId,"Available",false,true,"InspectionApproved",true,null)};
}
