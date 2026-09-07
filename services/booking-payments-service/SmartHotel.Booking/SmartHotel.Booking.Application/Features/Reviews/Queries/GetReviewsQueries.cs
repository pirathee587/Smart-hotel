using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Reviews.DTOs;
using SmartHotel.Booking.Application.Interfaces;

namespace SmartHotel.Booking.Application.Features.Reviews.Queries;

public record GetReviewsByRoomTypeQuery(Guid RoomTypeId) : IRequest<Result<List<ReviewDto>>>;

public class GetReviewsByRoomTypeQueryHandler : IRequestHandler<GetReviewsByRoomTypeQuery, Result<List<ReviewDto>>>
{
    private readonly IBookingDbContext _context;

    public GetReviewsByRoomTypeQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ReviewDto>>> Handle(GetReviewsByRoomTypeQuery query, CancellationToken ct)
    {
        var reviews = await _context.Reviews
            .Where(r => r.RoomTypeId == query.RoomTypeId && r.IsPublished)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                BookingId = r.BookingId,
                CustomerId = r.CustomerId,
                RoomTypeId = r.RoomTypeId,
                Rating = r.Rating,
                Comment = r.Comment,
                IsPublished = r.IsPublished,
                CreatedAtUtc = r.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Result<List<ReviewDto>>.Success(reviews);
    }
}

public record GetAllReviewsQuery(bool? IsPublished = null) : IRequest<Result<List<ReviewDto>>>;

public class GetAllReviewsQueryHandler : IRequestHandler<GetAllReviewsQuery, Result<List<ReviewDto>>>
{
    private readonly IBookingDbContext _context;

    public GetAllReviewsQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ReviewDto>>> Handle(GetAllReviewsQuery query, CancellationToken ct)
    {
        var dbQuery = _context.Reviews.AsQueryable();

        if (query.IsPublished.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.IsPublished == query.IsPublished.Value);
        }

        var reviews = await dbQuery
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                BookingId = r.BookingId,
                CustomerId = r.CustomerId,
                RoomTypeId = r.RoomTypeId,
                Rating = r.Rating,
                Comment = r.Comment,
                IsPublished = r.IsPublished,
                CreatedAtUtc = r.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Result<List<ReviewDto>>.Success(reviews);
    }
}
