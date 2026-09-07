using System.Text.Json;
using MediatR;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Complaints.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Complaints.Commands;

public record FileComplaintRequest
{
    public Guid? BookingId { get; init; }
    public ComplaintCategory Category { get; init; } = ComplaintCategory.Other;
    public ComplaintSeverity Severity { get; init; } = ComplaintSeverity.Medium;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public record FileComplaintCommand(FileComplaintRequest Request, Guid CustomerId) : IRequest<Result<ComplaintDto>>;

public class FileComplaintCommandHandler : IRequestHandler<FileComplaintCommand, Result<ComplaintDto>>
{
    private readonly IBookingDbContext _context;

    public FileComplaintCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ComplaintDto>> Handle(FileComplaintCommand command, CancellationToken ct)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.Title))
        {
            return Result<ComplaintDto>.Failure("Complaint title is required.");
        }

        if (string.IsNullOrWhiteSpace(req.Description))
        {
            return Result<ComplaintDto>.Failure("Complaint description is required.");
        }

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            BookingId = req.BookingId,
            CustomerId = command.CustomerId,
            Category = req.Category,
            Severity = req.Severity,
            Status = ComplaintStatus.Open,
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            SlaDeadlineUtc = Complaint.CalculateSlaDeadline(req.Severity)
        };

        var initialEntry = new ComplaintTimelineEntry
        {
            Id = Guid.NewGuid(),
            ComplaintId = complaint.Id,
            FromStatus = ComplaintStatus.Open,
            ToStatus = ComplaintStatus.Open,
            Note = "Complaint submitted by customer.",
            ChangedBy = "Customer",
            TimestampUtc = DateTime.UtcNow
        };
        complaint.Timeline.Add(initialEntry);

        _context.Complaints.Add(complaint);

        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "complaint.filed",
            Content = JsonSerializer.Serialize(new
            {
                ComplaintId = complaint.Id,
                BookingId = complaint.BookingId,
                CustomerId = complaint.CustomerId,
                Category = complaint.Category.ToString(),
                Severity = complaint.Severity.ToString(),
                SlaDeadlineUtc = complaint.SlaDeadlineUtc,
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
            CreatedAtUtc = complaint.CreatedAtUtc,
            Timeline = new List<ComplaintTimelineEntryDto>
            {
                new ComplaintTimelineEntryDto
                {
                    Id = initialEntry.Id,
                    FromStatus = initialEntry.FromStatus,
                    ToStatus = initialEntry.ToStatus,
                    Note = initialEntry.Note,
                    ChangedBy = initialEntry.ChangedBy,
                    TimestampUtc = initialEntry.TimestampUtc
                }
            }
        };

        return Result<ComplaintDto>.Success(dto, "Complaint filed successfully.");
    }
}
