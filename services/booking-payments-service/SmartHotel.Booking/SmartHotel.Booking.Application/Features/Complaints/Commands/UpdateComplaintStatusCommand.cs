using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Complaints.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Complaints.Commands;

public record UpdateComplaintStatusRequest
{
    public ComplaintStatus Status { get; init; }
    public string? Note { get; init; }
    public string? ResolutionNotes { get; init; }
}

public record UpdateComplaintStatusCommand(Guid ComplaintId, UpdateComplaintStatusRequest Request, string ChangedBy) : IRequest<Result<ComplaintDto>>;

public class UpdateComplaintStatusCommandHandler : IRequestHandler<UpdateComplaintStatusCommand, Result<ComplaintDto>>
{
    private readonly IBookingDbContext _context;

    public UpdateComplaintStatusCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ComplaintDto>> Handle(UpdateComplaintStatusCommand command, CancellationToken ct)
    {
        var complaint = await _context.Complaints
            .Include(c => c.Timeline)
            .FirstOrDefaultAsync(c => c.Id == command.ComplaintId, ct);

        if (complaint == null)
        {
            return Result<ComplaintDto>.Failure($"Complaint with ID {command.ComplaintId} was not found.");
        }

        var req = command.Request;

        switch (req.Status)
        {
            case ComplaintStatus.Resolved:
                if (string.IsNullOrWhiteSpace(req.ResolutionNotes))
                {
                    return Result<ComplaintDto>.Failure("Resolution notes are required when marking a complaint as Resolved.");
                }
                complaint.Resolve(req.ResolutionNotes, command.ChangedBy);
                break;

            case ComplaintStatus.Escalated:
                complaint.Escalate(req.Note ?? "Manually escalated by staff.", command.ChangedBy);
                break;

            case ComplaintStatus.Closed:
                complaint.Close(command.ChangedBy);
                break;

            default:
                complaint.AddTimelineEntry(req.Status, req.Note ?? $"Status updated to {req.Status}", command.ChangedBy);
                break;
        }

        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "complaint.updated",
            Content = JsonSerializer.Serialize(new
            {
                ComplaintId = complaint.Id,
                Status = complaint.Status.ToString(),
                ChangedBy = command.ChangedBy,
                Note = req.Note ?? req.ResolutionNotes,
                OccurredOnUtc = DateTime.UtcNow
            })
        });

        await _context.SaveChangesAsync(ct);

        var dto = new ComplaintDto
        {
            Id = complaint.Id,
            BookingId = complaint.BookingId,
            CustomerId = complaint.CustomerId,
            Category = complaint.Category,
            Severity = complaint.Severity,
            Status = complaint.Status,
            Title = complaint.Title,
            Description = complaint.Description,
            SlaDeadlineUtc = complaint.SlaDeadlineUtc,
            ResolutionNotes = complaint.ResolutionNotes,
            ResolvedAtUtc = complaint.ResolvedAtUtc,
            CreatedAtUtc = complaint.CreatedAtUtc,
            Timeline = complaint.Timeline.OrderBy(t => t.TimestampUtc).Select(t => new ComplaintTimelineEntryDto
            {
                Id = t.Id,
                FromStatus = t.FromStatus,
                ToStatus = t.ToStatus,
                Note = t.Note,
                ChangedBy = t.ChangedBy,
                TimestampUtc = t.TimestampUtc
            }).ToList()
        };

        return Result<ComplaintDto>.Success(dto, $"Complaint status updated to {complaint.Status}.");
    }
}
