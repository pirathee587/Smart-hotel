using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Features.Reviews.Commands;
using SmartHotel.Booking.Application.Features.Reviews.Queries;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class ReviewRulesTests
{
    private BookingDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task CreateReview_BookingNotCheckedOut_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-REV01",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Jayawardena",
            CustomerEmail = "jaya@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = BookingStatus.Confirmed // Confirmed, but not yet CheckedOut!
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var handler = new CreateReviewCommandHandler(context);
        var req = new CreateReviewRequest
        {
            BookingId = booking.Id,
            Rating = 5,
            Comment = "Loved the stay!"
        };

        var result = await handler.Handle(new CreateReviewCommand(req, booking.CustomerId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Reviews can only be submitted after check-out");
    }

    [Fact]
    public async Task CreateReview_CheckedOutBooking_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-REV02",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Jayawardena",
            CustomerEmail = "jaya@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Status = BookingStatus.CheckedOut
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var handler = new CreateReviewCommandHandler(context);
        var req = new CreateReviewRequest
        {
            BookingId = booking.Id,
            Rating = 5,
            Comment = "Amazing hospitality and cleanliness!"
        };

        var result = await handler.Handle(new CreateReviewCommand(req, booking.CustomerId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Rating.Should().Be(5);
        result.Data.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task CreateReview_SecondReviewOnSameBooking_Rejected()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-REV03",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Jayawardena",
            CustomerEmail = "jaya@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Status = BookingStatus.CheckedOut
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var handler = new CreateReviewCommandHandler(context);
        var req = new CreateReviewRequest
        {
            BookingId = booking.Id,
            Rating = 4,
            Comment = "Good overall."
        };

        var result1 = await handler.Handle(new CreateReviewCommand(req, booking.CustomerId), CancellationToken.None);
        result1.Succeeded.Should().BeTrue();

        // Attempt second review for same booking
        var result2 = await handler.Handle(new CreateReviewCommand(req, booking.CustomerId), CancellationToken.None);
        result2.Succeeded.Should().BeFalse();
        result2.Message.Should().Contain("immutable");
    }

    [Fact]
    public async Task HideAndUnhideReview_TogglesVisibility()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var review = new Review
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            Rating = 1,
            Comment = "Inappropriate text",
            IsPublished = true
        };
        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        // Admin hides review
        var hideHandler = new HideReviewCommandHandler(context);
        var hideResult = await hideHandler.Handle(new HideReviewCommand(review.Id), CancellationToken.None);
        hideResult.Succeeded.Should().BeTrue();

        var hidden = await context.Reviews.FindAsync(review.Id);
        hidden!.IsPublished.Should().BeFalse();

        // Admin unhides review
        var unhideHandler = new UnhideReviewCommandHandler(context);
        var unhideResult = await unhideHandler.Handle(new UnhideReviewCommand(review.Id), CancellationToken.None);
        unhideResult.Succeeded.Should().BeTrue();

        var unhidden = await context.Reviews.FindAsync(review.Id);
        unhidden!.IsPublished.Should().BeTrue();
    }
}
