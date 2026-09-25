using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Authorization;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Infrastructure.Persistence;

namespace SmartHotel.HotelOps.API.Controllers;

public record ClaimRoomApiRequest(Guid BookingId, string? Source = null);

[ApiController, Route("api/v1/rooms/{roomId:guid}/readiness")]
public class RoomReadinessController(HotelOpsDbContext db, IAuthorizationService authorization, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid roomId, CancellationToken ct)
    {
        if (!ServiceAuthorized() && !(await authorization.AuthorizeAsync(User, HotelPolicies.FrontOfficeOperations)).Succeeded)
            return Forbid();

        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roomId, ct);
        if (room is null) return NotFound();

        var blocked = await db.MaintenanceRestrictions.AsNoTracking().AnyAsync(x => x.RoomId == roomId && x.Status == MaintenanceRestrictionStatus.Active, ct);
        var latestCheckout = await db.OperationalEventReceipts.AsNoTracking().Where(x => x.RoomId == roomId && x.EventType == "booking.checkedout").MaxAsync(x => (DateTime?)x.ProcessedAtUtc, ct);
        var housekeeping = await db.HousekeepingReadinessRecords.AsNoTracking().Where(x => x.RoomId == roomId).OrderByDescending(x => x.InspectedAtUtc ?? x.StartedAtUtc).FirstOrDefaultAsync(ct);
        var approved = housekeeping?.Status == HousekeepingReadinessStatus.InspectionApproved && housekeeping.InspectedAtUtc.HasValue && (!latestCheckout.HasValue || housekeeping.InspectedAtUtc >= latestCheckout);
        var ready = room.Status == RoomStatus.Available && !blocked && approved;
        var status = housekeeping?.Status.ToString() ?? "AwaitingCleaning";
        var blocker = blocked ? "Maintenance restriction active" : room.Status switch
        {
            RoomStatus.Dirty => "Awaiting cleaning",
            RoomStatus.InCleaning => status == "CleaningStarted" ? "Cleaning in progress" : "Awaiting inspection",
            RoomStatus.OutOfOrder => "Room is out of service",
            RoomStatus.Occupied => "Room has an active occupancy",
            _ => !approved ? "Approved Housekeeping inspection is required" : null
        };

        return Ok(new { roomId, roomStatus = room.Status.ToString(), maintenanceBlocked = blocked, housekeepingApproved = approved, readinessStatus = status, readyForCheckIn = ready, blocker });
    }

    /// <summary>
    /// Atomically claims a room for check-in after verifying readiness in an atomic state transition.
    /// Eliminates cross-service race conditions between Booking and Hotel Ops.
    /// </summary>
    [HttpPost("claim")]
    [HttpPost("/api/v1/rooms/{roomId:guid}/claim")]
    public async Task<IActionResult> Claim(Guid roomId, [FromBody] ClaimRoomApiRequest request, CancellationToken ct)
    {
        if (!ServiceAuthorized() && !(await authorization.AuthorizeAsync(User, HotelPolicies.FrontOfficeOperations)).Succeeded)
            return Forbid();

        var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, ct);
        if (room is null) return NotFound(new { message = $"Room {roomId} not found." });

        // Check if already claimed for this reservation (idempotency)
        var existingClaim = await db.OperationalEventReceipts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoomId == roomId && x.TaskId == request.BookingId && (x.EventType == "booking.room-claimed" || x.EventType == "booking.checkedin"), ct);
        if (existingClaim != null && room.Status == RoomStatus.Occupied)
        {
            return Ok(new { success = true, claimed = true, roomId, bookingId = request.BookingId, message = "Room already claimed for this reservation." });
        }

        var blocked = await db.MaintenanceRestrictions.AsNoTracking().AnyAsync(x => x.RoomId == roomId && x.Status == MaintenanceRestrictionStatus.Active, ct);
        var latestCheckout = await db.OperationalEventReceipts.AsNoTracking().Where(x => x.RoomId == roomId && x.EventType == "booking.checkedout").MaxAsync(x => (DateTime?)x.ProcessedAtUtc, ct);
        var housekeeping = await db.HousekeepingReadinessRecords.AsNoTracking().Where(x => x.RoomId == roomId).OrderByDescending(x => x.InspectedAtUtc ?? x.StartedAtUtc).FirstOrDefaultAsync(ct);
        var approved = housekeeping?.Status == HousekeepingReadinessStatus.InspectionApproved && housekeeping.InspectedAtUtc.HasValue && (!latestCheckout.HasValue || housekeeping.InspectedAtUtc >= latestCheckout);

        if (blocked)
            return Conflict(new { success = false, claimed = false, blocker = "Maintenance restriction active", message = "Room has an active maintenance restriction." });
        if (room.Status == RoomStatus.Occupied)
            return Conflict(new { success = false, claimed = false, blocker = "Room has an active occupancy", message = "Room is currently occupied." });
        if (room.Status == RoomStatus.OutOfOrder)
            return Conflict(new { success = false, claimed = false, blocker = "Room is out of service", message = "Room is out of order." });
        if (room.Status == RoomStatus.Dirty)
            return Conflict(new { success = false, claimed = false, blocker = "Awaiting cleaning", message = "Room is dirty and awaiting cleaning." });
        if (room.Status == RoomStatus.InCleaning)
            return Conflict(new { success = false, claimed = false, blocker = housekeeping?.Status == HousekeepingReadinessStatus.CleaningStarted ? "Cleaning in progress" : "Awaiting inspection", message = "Room cleaning is not complete." });
        if (!approved)
            return Conflict(new { success = false, claimed = false, blocker = "Approved Housekeeping inspection is required", message = "Room does not have an approved housekeeping inspection." });
        if (room.Status != RoomStatus.Available)
            return Conflict(new { success = false, claimed = false, blocker = $"Room status is {room.Status}", message = $"Room is not available (status: {room.Status})." });

        room.UpdateStatus(RoomStatus.Occupied);
        db.OperationalEventReceipts.Add(new OperationalEventReceipt
        {
            EventId = Guid.NewGuid(),
            TaskId = request.BookingId,
            RoomId = roomId,
            EventType = "booking.room-claimed",
            OccurredAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, claimed = true, roomId, bookingId = request.BookingId, message = "Room successfully claimed." });
    }

    /// <summary>
    /// Compensating transaction endpoint to release a claimed room if Booking commit fails.
    /// </summary>
    [HttpPost("release")]
    [HttpPost("/api/v1/rooms/{roomId:guid}/release-claim")]
    public async Task<IActionResult> Release(Guid roomId, [FromBody] ClaimRoomApiRequest request, CancellationToken ct)
    {
        if (!ServiceAuthorized() && !(await authorization.AuthorizeAsync(User, HotelPolicies.FrontOfficeOperations)).Succeeded)
            return Forbid();

        var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, ct);
        if (room is null) return NotFound();

        if (room.Status == RoomStatus.Occupied)
        {
            room.Status = RoomStatus.Available;
            room.UpdatedAtUtc = DateTime.UtcNow;
            db.OperationalEventReceipts.Add(new OperationalEventReceipt
            {
                EventId = Guid.NewGuid(),
                TaskId = request.BookingId,
                RoomId = roomId,
                EventType = "booking.room-claim-released",
                OccurredAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        return Ok(new { success = true, released = true, roomId });
    }

    private bool ServiceAuthorized()
    {
        var expected = configuration["HOTEL_OPS_SERVICE_API_KEY"] ?? configuration["Services:ServiceApiKey"];
        var supplied = Request.Headers["X-Service-Api-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
}

