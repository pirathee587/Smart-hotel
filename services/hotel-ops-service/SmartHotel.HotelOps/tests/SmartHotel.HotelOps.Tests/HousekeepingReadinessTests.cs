using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.API.Services;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Infrastructure.Persistence;

namespace SmartHotel.HotelOps.Tests;

public class HousekeepingReadinessTests
{
    [Fact]
    public async Task StartThenApprovedInspection_TransitionsDirtyToInCleaningToAvailable()
    {
        await using var db=Context(); var room=Room(RoomStatus.Dirty); db.Rooms.Add(room); await db.SaveChangesAsync(); var service=new HousekeepingReadinessService(db);
        var task=Guid.NewGuid(); var department=Guid.NewGuid(); var started=DateTime.UtcNow;
        var start=await service.StartAsync(room.Id,new(task,Guid.NewGuid(),department,Guid.NewGuid(),started),default);
        start.RoomStatus.Should().Be("InCleaning");
        var approved=await service.InspectAsync(room.Id,new(task,Guid.NewGuid(),department,Guid.NewGuid(),true,"Approved",started.AddMinutes(30)),default);
        approved.RoomStatus.Should().Be("Available"); approved.ReadinessStatus.Should().Be("InspectionApproved");
    }

    [Fact]
    public async Task DuplicateEvent_IsIdempotent()
    {
        await using var db=Context(); var room=Room(RoomStatus.Dirty); db.Rooms.Add(room); await db.SaveChangesAsync(); var service=new HousekeepingReadinessService(db); var eventId=Guid.NewGuid(); var task=Guid.NewGuid(); var department=Guid.NewGuid(); var request=new CleaningStartedRequest(task,eventId,department,Guid.NewGuid(),DateTime.UtcNow);
        await service.StartAsync(room.Id,request,default); var duplicate=await service.StartAsync(room.Id,request,default); duplicate.Idempotent.Should().BeTrue();
    }

    [Fact]
    public async Task ApprovalWithoutCleaningStart_IsRejected()
    {
        await using var db=Context(); var room=Room(RoomStatus.Dirty); db.Rooms.Add(room); await db.SaveChangesAsync(); var service=new HousekeepingReadinessService(db);
        var act=()=>service.InspectAsync(room.Id,new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),true,"invalid",DateTime.UtcNow),default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No cleaning-start*");
    }

    [Fact]
    public async Task OutOfOrderRoom_BlocksCleaningReadiness()
    {
        await using var db=Context(); var room=Room(RoomStatus.OutOfOrder); db.Rooms.Add(room); await db.SaveChangesAsync(); var service=new HousekeepingReadinessService(db);
        var act=()=>service.StartAsync(room.Id,new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),DateTime.UtcNow),default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Maintenance clearance*");
    }

    [Fact]
    public async Task RejectedInspection_RequiresRecleanBeforeApproval()
    {
        await using var db=Context(); var room=Room(RoomStatus.Dirty); db.Rooms.Add(room); await db.SaveChangesAsync(); var service=new HousekeepingReadinessService(db); var task=Guid.NewGuid(); var department=Guid.NewGuid(); var employee=Guid.NewGuid(); var start=DateTime.UtcNow;
        await service.StartAsync(room.Id,new(task,Guid.NewGuid(),department,employee,start),default);
        await service.InspectAsync(room.Id,new(task,Guid.NewGuid(),department,Guid.NewGuid(),false,"Re-clean bathroom",start.AddMinutes(20)),default);
        var premature=()=>service.InspectAsync(room.Id,new(task,Guid.NewGuid(),department,Guid.NewGuid(),true,"approve",start.AddMinutes(21)),default);
        await premature.Should().ThrowAsync<InvalidOperationException>().WithMessage("*out of order*");
        await service.StartAsync(room.Id,new(task,Guid.NewGuid(),department,employee,start.AddMinutes(22)),default);
        var approved=await service.InspectAsync(room.Id,new(task,Guid.NewGuid(),department,Guid.NewGuid(),true,"Approved",start.AddMinutes(40)),default); approved.RoomStatus.Should().Be("Available");
    }

    private static HotelOpsDbContext Context()=>new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Room Room(RoomStatus status)=>new(){Id=Guid.NewGuid(),HotelId=Guid.NewGuid(),RoomTypeId=Guid.NewGuid(),RoomNumber="T-"+Guid.NewGuid().ToString("N")[..5],Status=status};
}
