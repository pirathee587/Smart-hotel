using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Infrastructure.Persistence;

namespace SmartHotel.HotelOps.API.Services;

public record CleaningStartedRequest(Guid TaskId, Guid EventId, Guid DepartmentId, Guid HousekeeperId, DateTime OccurredAtUtc);
public record InspectionDecisionRequest(Guid TaskId, Guid EventId, Guid DepartmentId, Guid ManagerId, bool Approved, string Notes, DateTime OccurredAtUtc);
public record RoomReadinessResponse(Guid RoomId, Guid TaskId, string RoomStatus, string ReadinessStatus, string ReadinessGate, bool Idempotent);

public sealed class HousekeepingReadinessService(HotelOpsDbContext db)
{
    public async Task<RoomReadinessResponse> StartAsync(Guid roomId, CleaningStartedRequest request, CancellationToken ct)
    {
        var prior = await db.OperationalEventReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == request.EventId, ct);
        if (prior is not null) return await Response(roomId, request.TaskId, true, ct);

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, ct) ?? throw new KeyNotFoundException("Room not found.");
        var readiness = await db.HousekeepingReadinessRecords.FirstOrDefaultAsync(x => x.TaskId == request.TaskId, ct);
        var conflicting = await db.HousekeepingReadinessRecords.AnyAsync(x => x.RoomId == roomId && x.TaskId != request.TaskId && x.Status != HousekeepingReadinessStatus.InspectionApproved, ct);
        if (conflicting) throw new InvalidOperationException("Another cleaning task is already active for this room.");
        if (room.Status == RoomStatus.OutOfOrder || await db.MaintenanceRestrictions.AnyAsync(x=>x.RoomId==roomId&&x.Status==MaintenanceRestrictionStatus.Active,ct)) throw new InvalidOperationException("Room is blocked until Maintenance clearance is authorized.");

        if (readiness is null)
        {
            if (room.Status != RoomStatus.Dirty) throw new InvalidOperationException("Cleaning can start only while the authoritative room state is Dirty.");
            readiness = new HousekeepingReadiness { TaskId=request.TaskId, RoomId=roomId, DepartmentId=request.DepartmentId, HousekeeperId=request.HousekeeperId, Status=HousekeepingReadinessStatus.CleaningStarted, StartedAtUtc=request.OccurredAtUtc };
            db.HousekeepingReadinessRecords.Add(readiness);
            room.UpdateStatus(RoomStatus.InCleaning);
        }
        else
        {
            if (readiness.RoomId != roomId || readiness.DepartmentId != request.DepartmentId) throw new InvalidOperationException("Cleaning task does not match the room or department readiness record.");
            if (readiness.Status != HousekeepingReadinessStatus.RecleanRequired || room.Status != RoomStatus.InCleaning) throw new InvalidOperationException("Duplicate or out-of-order cleaning start event.");
            if (request.OccurredAtUtc <= readiness.StartedAtUtc) throw new InvalidOperationException("Stale cleaning event rejected.");
            readiness.Status=HousekeepingReadinessStatus.CleaningStarted; readiness.HousekeeperId=request.HousekeeperId; readiness.StartedAtUtc=request.OccurredAtUtc; readiness.UpdatedAtUtc=DateTime.UtcNow;
        }
        db.OperationalEventReceipts.Add(new OperationalEventReceipt { EventId=request.EventId, TaskId=request.TaskId, RoomId=roomId, EventType="housekeeping.cleaning_started", OccurredAtUtc=request.OccurredAtUtc });
        await db.SaveChangesAsync(ct); if(transaction is not null) await transaction.CommitAsync(ct);
        return new(roomId,request.TaskId,room.Status.ToString(),readiness.Status.ToString(),readiness.ReadinessGate,false);
    }

    public async Task<RoomReadinessResponse> InspectAsync(Guid roomId, InspectionDecisionRequest request, CancellationToken ct)
    {
        var prior = await db.OperationalEventReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == request.EventId, ct);
        if (prior is not null) return await Response(roomId, request.TaskId, true, ct);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, ct) ?? throw new KeyNotFoundException("Room not found.");
        var readiness = await db.HousekeepingReadinessRecords.FirstOrDefaultAsync(x => x.TaskId == request.TaskId, ct) ?? throw new InvalidOperationException("No cleaning-start readiness record exists for this task.");
        if (readiness.RoomId != roomId || readiness.DepartmentId != request.DepartmentId) throw new InvalidOperationException("Inspection does not match the cleaning task, room, or department.");
        if (request.OccurredAtUtc <= readiness.StartedAtUtc) throw new InvalidOperationException("Stale inspection event rejected.");
        var maintenanceBlocked=await db.MaintenanceRestrictions.AnyAsync(x=>x.RoomId==roomId&&x.Status==MaintenanceRestrictionStatus.Active,ct);
        if ((!maintenanceBlocked && room.Status != RoomStatus.InCleaning) || readiness.Status != HousekeepingReadinessStatus.CleaningStarted) throw new InvalidOperationException("Inspection is out of order or conflicts with authoritative occupancy state.");

        readiness.ManagerId=request.ManagerId; readiness.InspectedAtUtc=request.OccurredAtUtc; readiness.InspectionNotes=request.Notes; readiness.UpdatedAtUtc=DateTime.UtcNow;
        if (request.Approved) { if(!maintenanceBlocked){room.UpdateStatus(RoomStatus.Inspected); room.UpdateStatus(RoomStatus.Available);} readiness.Status=HousekeepingReadinessStatus.InspectionApproved; }
        else readiness.Status=HousekeepingReadinessStatus.RecleanRequired;
        db.OperationalEventReceipts.Add(new OperationalEventReceipt { EventId=request.EventId, TaskId=request.TaskId, RoomId=roomId, EventType=request.Approved?"housekeeping.inspection_approved":"housekeeping.inspection_rejected", OccurredAtUtc=request.OccurredAtUtc });
        await db.SaveChangesAsync(ct); if(transaction is not null) await transaction.CommitAsync(ct);
        return new(roomId,request.TaskId,room.Status.ToString(),readiness.Status.ToString(),readiness.ReadinessGate,false);
    }

    private async Task<RoomReadinessResponse> Response(Guid roomId, Guid taskId, bool idempotent, CancellationToken ct)
    {
        var room=await db.Rooms.AsNoTracking().SingleAsync(x=>x.Id==roomId,ct); var readiness=await db.HousekeepingReadinessRecords.AsNoTracking().SingleAsync(x=>x.TaskId==taskId,ct);
        return new(roomId,taskId,room.Status.ToString(),readiness.Status.ToString(),readiness.ReadinessGate,idempotent);
    }
}
