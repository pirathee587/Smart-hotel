using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Complaints.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Complaints.Queries;

public record GetCustomerComplaintsQuery(Guid CustomerId) : IRequest<Result<List<ComplaintDto>>>;

public class GetCustomerComplaintsQueryHandler : IRequestHandler<GetCustomerComplaintsQuery, Result<List<ComplaintDto>>>
{
    private readonly IBookingDbContext _context;

    public GetCustomerComplaintsQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ComplaintDto>>> Handle(GetCustomerComplaintsQuery query, CancellationToken ct)
    {
        var complaints = await _context.Complaints
            .Include(c => c.Timeline)
            .Where(c => c.CustomerId == query.CustomerId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new ComplaintDto
            {
                Id = c.Id,
                BookingId = c.BookingId,
                CustomerId = c.CustomerId,
                Category = c.Category,
                Severity = c.Severity,
                Status = c.Status,
                Title = c.Title,
                Description = c.Description,
                SlaDeadlineUtc = c.SlaDeadlineUtc,
                ResolutionNotes = c.ResolutionNotes,
                ResolvedAtUtc = c.ResolvedAtUtc,
                CreatedAtUtc = c.CreatedAtUtc,
                Timeline = c.Timeline.OrderBy(t => t.TimestampUtc).Select(t => new ComplaintTimelineEntryDto
                {
                    Id = t.Id,
                    FromStatus = t.FromStatus,
                    ToStatus = t.ToStatus,
                    Note = t.Note,
                    ChangedBy = t.ChangedBy,
                    TimestampUtc = t.TimestampUtc
                }).ToList()
            })
            .ToListAsync(ct);

        return Result<List<ComplaintDto>>.Success(complaints);
    }
}

public record GetComplaintsQuery(ComplaintStatus? Status = null, ComplaintSeverity? Severity = null, bool OverdueOnly = false) : IRequest<Result<List<ComplaintDto>>>;

public class GetComplaintsQueryHandler : IRequestHandler<GetComplaintsQuery, Result<List<ComplaintDto>>>
{
    private readonly IBookingDbContext _context;

    public GetComplaintsQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ComplaintDto>>> Handle(GetComplaintsQuery query, CancellationToken ct)
    {
        var dbQuery = _context.Complaints.Include(c => c.Timeline).AsQueryable();

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.Status == query.Status.Value);
        }

        if (query.Severity.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.Severity == query.Severity.Value);
        }

        if (query.OverdueOnly)
        {
            var now = DateTime.UtcNow;
            dbQuery = dbQuery.Where(c => (c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.InProgress) && c.SlaDeadlineUtc < now);
        }

        var complaints = await dbQuery
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new ComplaintDto
            {
                Id = c.Id,
                BookingId = c.BookingId,
                CustomerId = c.CustomerId,
                Category = c.Category,
                Severity = c.Severity,
                Status = c.Status,
                Title = c.Title,
                Description = c.Description,
                SlaDeadlineUtc = c.SlaDeadlineUtc,
                ResolutionNotes = c.ResolutionNotes,
                ResolvedAtUtc = c.ResolvedAtUtc,
                CreatedAtUtc = c.CreatedAtUtc,
                Timeline = c.Timeline.OrderBy(t => t.TimestampUtc).Select(t => new ComplaintTimelineEntryDto
                {
                    Id = t.Id,
                    FromStatus = t.FromStatus,
                    ToStatus = t.ToStatus,
                    Note = t.Note,
                    ChangedBy = t.ChangedBy,
                    TimestampUtc = t.TimestampUtc
                }).ToList()
            })
            .ToListAsync(ct);

        return Result<List<ComplaintDto>>.Success(complaints);
    }
}

public record GetComplaintByIdQuery(Guid Id) : IRequest<Result<ComplaintDto>>;

public class GetComplaintByIdQueryHandler : IRequestHandler<GetComplaintByIdQuery, Result<ComplaintDto>>
{
    private readonly IBookingDbContext _context;

    public GetComplaintByIdQueryHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ComplaintDto>> Handle(GetComplaintByIdQuery query, CancellationToken ct)
    {
        var c = await _context.Complaints
            .Include(c => c.Timeline)
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);

        if (c == null)
        {
            return Result<ComplaintDto>.Failure($"Complaint with ID {query.Id} was not found.");
        }

        var dto = new ComplaintDto
        {
            Id = c.Id,
            BookingId = c.BookingId,
            CustomerId = c.CustomerId,
            Category = c.Category,
            Severity = c.Severity,
            Status = c.Status,
            Title = c.Title,
            Description = c.Description,
            SlaDeadlineUtc = c.SlaDeadlineUtc,
            ResolutionNotes = c.ResolutionNotes,
            ResolvedAtUtc = c.ResolvedAtUtc,
            CreatedAtUtc = c.CreatedAtUtc,
            Timeline = c.Timeline.OrderBy(t => t.TimestampUtc).Select(t => new ComplaintTimelineEntryDto
            {
                Id = t.Id,
                FromStatus = t.FromStatus,
                ToStatus = t.ToStatus,
                Note = t.Note,
                ChangedBy = t.ChangedBy,
                TimestampUtc = t.TimestampUtc
            }).ToList()
        };

        return Result<ComplaintDto>.Success(dto);
    }
}
