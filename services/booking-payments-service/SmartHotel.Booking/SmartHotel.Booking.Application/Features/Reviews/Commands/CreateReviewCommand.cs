using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Reviews.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Reviews.Commands;

public record CreateReviewRequest
{
    public Guid BookingId { get; init; }
    public int Rating { get; init; }
    public string Comment { get; init; } = string.Empty;
}

public record CreateReviewCommand(CreateReviewRequest Request, Guid? CustomerId = null) : IRequest<Result<ReviewDto>>;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, Result<ReviewDto>>
{
    private readonly IBookingDbContext _context;

    public CreateReviewCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ReviewDto>> Handle(CreateReviewCommand command, CancellationToken ct)
    {
        var req = command.Request;

        if (req.Rating < 1 || req.Rating > 5)
        {
            return Result<ReviewDto>.Failure("Rating must be between 1 and 5 stars.");
        }

        if (string.IsNullOrWhiteSpace(req.Comment))
        {
            return Result<ReviewDto>.Failure("Review comment is required.");
        }

        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == req.BookingId, ct);
        if (booking == null)
        {
            return Result<ReviewDto>.Failure($"Booking with ID {req.BookingId} was not found.");
        }

        if (command.CustomerId.HasValue && booking.CustomerId != command.CustomerId.Value)
        {
            return Result<ReviewDto>.Failure("You can only review bookings associated with your account.");
        }

        // Reviews are only permitted after guest has checked out
        if (booking.Status != BookingStatus.CheckedOut)
        {
            return Result<ReviewDto>.Failure($"Reviews can only be submitted after check-out. Current booking status is {booking.Status}.");
        }

        // Check if review already exists for this booking (1 review per completed booking rule)
        var existingReview = await _context.Reviews.AnyAsync(r => r.BookingId == req.BookingId, ct);
        if (existingReview)
        {
            return Result<ReviewDto>.Failure("A review has already been submitted for this booking. Reviews are immutable.");
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            CustomerId = booking.CustomerId,
            RoomTypeId = booking.RoomTypeId,
            Rating = req.Rating,
            Comment = req.Comment.Trim(),
            IsPublished = true
        };

        _context.Reviews.Add(review);

        // Outbox event for review submission
        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "review.submitted",
            Content = JsonSerializer.Serialize(new
            {
                ReviewId = review.Id,
                BookingId = review.BookingId,
                CustomerId = review.CustomerId,
                RoomTypeId = review.RoomTypeId,
                Rating = review.Rating,
                OccurredOnUtc = DateTime.UtcNow
            })
        });

        await _context.SaveChangesAsync(ct);

        var dto = new ReviewDto
        {
            Id = review.Id,
            BookingId = review.BookingId,
            CustomerId = review.CustomerId,
            RoomTypeId = review.RoomTypeId,
            Rating = review.Rating,
            Comment = review.Comment,
            IsPublished = review.IsPublished,
            CreatedAtUtc = review.CreatedAtUtc
        };

        return Result<ReviewDto>.Success(dto, "Review submitted successfully.");
    }
}
