using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Infrastructure.Persistence;

namespace SmartHotel.HotelOps.API.Services;
public record RestrictRoomRequest(Guid WorkOrderId,Guid EventId,Guid DepartmentId,Guid ActorId,string Issue,string Severity,bool SafetyHazard,DateTime OccurredAtUtc);
public record ClearRoomRestrictionRequest(Guid WorkOrderId,Guid EventId,Guid DepartmentId,Guid ManagerId,DateTime OccurredAtUtc);
public record MaintenanceRestrictionResponse(Guid RoomId,Guid WorkOrderId,string RestrictionStatus,string RoomStatus,bool HousekeepingApproved,bool Idempotent);

public sealed class MaintenanceRestrictionService(HotelOpsDbContext db)
{
 public async Task<MaintenanceRestrictionResponse> Restrict(Guid roomId,RestrictRoomRequest r,CancellationToken ct){
  if(await db.OperationalEventReceipts.AsNoTracking().AnyAsync(x=>x.EventId==r.EventId,ct))return await Response(roomId,r.WorkOrderId,true,ct);
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;
  var room=await db.Rooms.FirstOrDefaultAsync(x=>x.Id==roomId,ct)??throw new KeyNotFoundException();
  var existing=await db.MaintenanceRestrictions.FirstOrDefaultAsync(x=>x.WorkOrderId==r.WorkOrderId,ct); if(existing is not null)return await Response(roomId,r.WorkOrderId,true,ct);
  if(await db.MaintenanceRestrictions.AnyAsync(x=>x.RoomId==roomId&&x.Status==MaintenanceRestrictionStatus.Active,ct))throw new InvalidOperationException("Room already has an active Maintenance restriction.");
  room.UpdateStatus(RoomStatus.OutOfOrder,string.IsNullOrWhiteSpace(r.Issue)?"Maintenance issue":r.Issue);
  db.MaintenanceRestrictions.Add(new(){WorkOrderId=r.WorkOrderId,RoomId=roomId,DepartmentId=r.DepartmentId,ReportedBy=r.ActorId,Issue=r.Issue,Severity=r.Severity,SafetyHazard=r.SafetyHazard,ReportedAtUtc=r.OccurredAtUtc});
  Receipt(r.EventId,r.WorkOrderId,roomId,"maintenance.restricted",r.OccurredAtUtc); await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);return await Response(roomId,r.WorkOrderId,false,ct);
 }
 public async Task<MaintenanceRestrictionResponse> Clear(Guid roomId,ClearRoomRestrictionRequest r,CancellationToken ct){
  if(await db.OperationalEventReceipts.AsNoTracking().AnyAsync(x=>x.EventId==r.EventId,ct))return await Response(roomId,r.WorkOrderId,true,ct);
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;
  var room=await db.Rooms.FirstOrDefaultAsync(x=>x.Id==roomId,ct)??throw new KeyNotFoundException();var restriction=await db.MaintenanceRestrictions.FirstOrDefaultAsync(x=>x.WorkOrderId==r.WorkOrderId&&x.RoomId==roomId,ct)??throw new InvalidOperationException("Maintenance restriction not found.");
  if(restriction.DepartmentId!=r.DepartmentId||restriction.Status!=MaintenanceRestrictionStatus.Active||room.Status!=RoomStatus.OutOfOrder)throw new InvalidOperationException("Restriction clearance is stale or out of order.");
  restriction.Status=MaintenanceRestrictionStatus.Cleared;restriction.ClearedBy=r.ManagerId;restriction.ClearedAtUtc=r.OccurredAtUtc;restriction.UpdatedAtUtc=DateTime.UtcNow;
  var approved=await db.HousekeepingReadinessRecords.AnyAsync(x=>x.RoomId==roomId&&x.Status==HousekeepingReadinessStatus.InspectionApproved&&x.InspectedAtUtc>=restriction.ReportedAtUtc,ct);
  room.UpdateStatus(approved?RoomStatus.Inspected:RoomStatus.Dirty);if(approved)room.UpdateStatus(RoomStatus.Available);
  Receipt(r.EventId,r.WorkOrderId,roomId,"maintenance.cleared",r.OccurredAtUtc);await db.SaveChangesAsync(ct);if(tx is not null)await tx.CommitAsync(ct);return await Response(roomId,r.WorkOrderId,false,ct);
 }
 public async Task<bool> IsBlocked(Guid roomId,CancellationToken ct)=>await db.MaintenanceRestrictions.AsNoTracking().AnyAsync(x=>x.RoomId==roomId&&x.Status==MaintenanceRestrictionStatus.Active,ct);
 private void Receipt(Guid e,Guid w,Guid room,string type,DateTime at)=>db.OperationalEventReceipts.Add(new(){EventId=e,TaskId=w,RoomId=room,EventType=type,OccurredAtUtc=at});
 private async Task<MaintenanceRestrictionResponse> Response(Guid roomId,Guid work,bool idem,CancellationToken ct){var room=await db.Rooms.AsNoTracking().SingleAsync(x=>x.Id==roomId,ct);var x=await db.MaintenanceRestrictions.AsNoTracking().SingleAsync(v=>v.WorkOrderId==work,ct);var approved=await db.HousekeepingReadinessRecords.AsNoTracking().AnyAsync(v=>v.RoomId==roomId&&v.Status==HousekeepingReadinessStatus.InspectionApproved&&v.InspectedAtUtc>=x.ReportedAtUtc,ct);return new(roomId,work,x.Status.ToString(),room.Status.ToString(),approved,idem);}
}
