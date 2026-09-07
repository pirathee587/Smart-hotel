using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Interfaces;

namespace SmartHotel.Booking.Application.Features.Reviews.Commands;

public record HideReviewCommand(Guid ReviewId) : IRequest<Result<bool>>;

public class HideReviewCommandHandler : IRequestHandler<HideReviewCommand, Result<bool>>
{
    private readonly IBookingDbContext _context;

    public HideReviewCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(HideReviewCommand command, CancellationToken ct)
    {
        var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == command.ReviewId, ct);
        if (review == null)
        {
            return Result<bool>.Failure($"Review with ID {command.ReviewId} was not found.");
        }

        review.Hide();
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true, "Review has been hidden from public display.");
    }
}

public record UnhideReviewCommand(Guid ReviewId) : IRequest<Result<bool>>;

public class UnhideReviewCommandHandler : IRequestHandler<UnhideReviewCommand, Result<bool>>
{
    private readonly IBookingDbContext _context;

    public UnhideReviewCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UnhideReviewCommand command, CancellationToken ct)
    {
        var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == command.ReviewId, ct);
        if (review == null)
        {
            return Result<bool>.Failure($"Review with ID {command.ReviewId} was not found.");
        }

        review.Unhide();
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true, "Review is now visible to the public.");
    }
}
