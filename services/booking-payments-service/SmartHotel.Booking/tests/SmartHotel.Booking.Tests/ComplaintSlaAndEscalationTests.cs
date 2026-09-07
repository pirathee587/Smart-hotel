using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Features.Complaints.Commands;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class ComplaintSlaAndEscalationTests
{
    private BookingDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Theory]
    [InlineData(ComplaintSeverity.Low, 48)]
    [InlineData(ComplaintSeverity.Medium, 24)]
    [InlineData(ComplaintSeverity.High, 6)]
    [InlineData(ComplaintSeverity.Critical, 2)]
    public void CalculateSlaDeadline_ComputesCorrectHoursBasedOnSeverity(ComplaintSeverity severity, int expectedHours)
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var deadline = Complaint.CalculateSlaDeadline(severity, now);

        deadline.Should().Be(now.AddHours(expectedHours));
    }

    [Fact]
    public async Task FileComplaint_CreatesComplaintWithSlaAndInitialTimeline()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var handler = new FileComplaintCommandHandler(context);
        var customerId = Guid.NewGuid();

        var req = new FileComplaintRequest
        {
            Category = ComplaintCategory.Maintenance,
            Severity = ComplaintSeverity.High,
            Title = "Air Conditioning Failure",
            Description = "AC in room 302 is blowing warm air."
        };

        var result = await handler.Handle(new FileComplaintCommand(req, customerId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Severity.Should().Be(ComplaintSeverity.High);
        result.Data.Status.Should().Be(ComplaintStatus.Open);
        result.Data.SlaDeadlineUtc.Should().BeCloseTo(DateTime.UtcNow.AddHours(6), TimeSpan.FromSeconds(5));
        result.Data.Timeline.Should().HaveCount(1);
        result.Data.Timeline[0].Note.Should().Contain("Complaint submitted");

        // Verify outbox message created
        var outbox = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "complaint.filed");
        outbox.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateComplaintStatus_Resolve_RequiresNotesAndAddsTimeline()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Category = ComplaintCategory.Cleanliness,
            Severity = ComplaintSeverity.Medium,
            Status = ComplaintStatus.InProgress,
            Title = "Extra Towels Needed",
            Description = "Requested extra towels 2 hours ago.",
            SlaDeadlineUtc = DateTime.UtcNow.AddHours(24)
        };
        context.Complaints.Add(complaint);
        await context.SaveChangesAsync();

        var handler = new UpdateComplaintStatusCommandHandler(context);

        // Attempt resolve without notes -> fails
        var failReq = new UpdateComplaintStatusRequest
        {
            Status = ComplaintStatus.Resolved,
            ResolutionNotes = ""
        };
        var failResult = await handler.Handle(new UpdateComplaintStatusCommand(complaint.Id, failReq, "FrontDesk"), CancellationToken.None);
        failResult.Succeeded.Should().BeFalse();

        // Resolve with notes -> succeeds
        var successReq = new UpdateComplaintStatusRequest
        {
            Status = ComplaintStatus.Resolved,
            ResolutionNotes = "Housekeeping delivered 4 fresh bath towels to room."
        };
        var successResult = await handler.Handle(new UpdateComplaintStatusCommand(complaint.Id, successReq, "FrontDesk"), CancellationToken.None);
        successResult.Succeeded.Should().BeTrue();
        successResult.Data!.Status.Should().Be(ComplaintStatus.Resolved);
        successResult.Data.ResolutionNotes.Should().Contain("Housekeeping delivered");
        successResult.Data.Timeline.Should().Contain(t => t.ToStatus == ComplaintStatus.Resolved);
    }

    [Fact]
    public async Task AutoEscalateOverdueComplaints_OverdueOpenComplaint_TransitionsToEscalated()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        // Complaint that breached SLA deadline 2 hours ago
        var overdueComplaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Category = ComplaintCategory.Staff,
            Severity = ComplaintSeverity.High,
            Status = ComplaintStatus.Open,
            Title = "Room Service Delinquent",
            Description = "Dinner ordered 3 hours ago never arrived.",
            SlaDeadlineUtc = DateTime.UtcNow.AddHours(-2) // Past deadline!
        };

        // Fresh complaint not overdue
        var freshComplaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Category = ComplaintCategory.Noise,
            Severity = ComplaintSeverity.Medium,
            Status = ComplaintStatus.Open,
            Title = "Loud music next door",
            Description = "Noise from 304.",
            SlaDeadlineUtc = DateTime.UtcNow.AddHours(20) // Future
        };

        context.Complaints.AddRange(overdueComplaint, freshComplaint);
        await context.SaveChangesAsync();

        var handler = new AutoEscalateOverdueComplaintsCommandHandler(context);
        var result = await handler.Handle(new AutoEscalateOverdueComplaintsCommand(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(1, "Exactly 1 overdue complaint should be auto-escalated");

        var updatedOverdue = await context.Complaints.Include(c => c.Timeline).FirstAsync(c => c.Id == overdueComplaint.Id);
        updatedOverdue.Status.Should().Be(ComplaintStatus.Escalated);
        updatedOverdue.Timeline.Should().Contain(t => t.ToStatus == ComplaintStatus.Escalated && t.ChangedBy == "SlaWorker");

        var updatedFresh = await context.Complaints.FirstAsync(c => c.Id == freshComplaint.Id);
        updatedFresh.Status.Should().Be(ComplaintStatus.Open);

        // Verify outbox message written
        var outbox = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "complaint.escalated");
        outbox.Should().NotBeNull();
    }
}
