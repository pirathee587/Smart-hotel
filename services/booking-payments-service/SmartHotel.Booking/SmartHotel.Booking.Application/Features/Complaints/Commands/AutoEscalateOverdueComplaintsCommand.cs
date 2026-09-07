using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Complaints.Commands;

public record AutoEscalateOverdueComplaintsCommand : IRequest<Result<int>>;

public class AutoEscalateOverdueComplaintsCommandHandler : IRequestHandler<AutoEscalateOverdueComplaintsCommand, Result<int>>
{
    private readonly IBookingDbContext _context;

    public AutoEscalateOverdueComplaintsCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Handle(AutoEscalateOverdueComplaintsCommand command, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Find active complaints where SLA deadline has passed and not already Escalated/Resolved/Closed
        var overdueComplaints = await _context.Complaints
            .Include(c => c.Timeline)
            .Where(c => (c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.InProgress) && c.SlaDeadlineUtc < now)
            .ToListAsync(ct);

        if (!overdueComplaints.Any())
        {
            return Result<int>.Success(0, "No overdue complaints found.");
        }

        foreach (var complaint in overdueComplaints)
        {
            complaint.Escalate("Auto-escalated due to SLA deadline breach.", "SlaWorker");

            _context.OutboxMessages.Add(new OutboxMessage
            {
                Type = "complaint.escalated",
                Content = JsonSerializer.Serialize(new
                {
                    ComplaintId = complaint.Id,
                    CustomerId = complaint.CustomerId,
                    BookingId = complaint.BookingId,
                    Severity = complaint.Severity.ToString(),
                    SlaDeadlineUtc = complaint.SlaDeadlineUtc,
                    BreachedAtUtc = now
                })
            });
        }

        await _context.SaveChangesAsync(ct);

        return Result<int>.Success(overdueComplaints.Count, $"{overdueComplaints.Count} overdue complaint(s) auto-escalated.");
    }
}
