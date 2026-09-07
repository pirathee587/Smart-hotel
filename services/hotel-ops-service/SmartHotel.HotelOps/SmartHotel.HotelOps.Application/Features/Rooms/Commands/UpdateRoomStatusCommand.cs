using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Rooms.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Domain.Exceptions;

namespace SmartHotel.HotelOps.Application.Features.Rooms.Commands;

public record UpdateRoomStatusCommand(Guid RoomId, RoomStatus NewStatus, string? Reason = null) : IRequest<Result<RoomDto>>;

public class UpdateRoomStatusCommandHandler : IRequestHandler<UpdateRoomStatusCommand, Result<RoomDto>>
{
    private readonly IHotelOpsDbContext _context;

    public UpdateRoomStatusCommandHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RoomDto>> Handle(UpdateRoomStatusCommand command, CancellationToken ct)
    {
        var room = await _context.Rooms
            .Include(r => r.RoomType)
            .FirstOrDefaultAsync(r => r.Id == command.RoomId, ct);

        if (room == null)
        {
            return Result<RoomDto>.Failure($"Room with ID {command.RoomId} was not found.");
        }

        var previousStatus = room.Status;

        try
        {
            // Execute domain state machine validation and transition
            room.UpdateStatus(command.NewStatus, command.Reason);
        }
        catch (InvalidRoomStatusTransitionException ex)
        {
            return Result<RoomDto>.Failure(ex.Message);
        }

        // Write transactional outbox message in the same unit of work
        var eventPayload = new
        {
            RoomId = room.Id,
            HotelId = room.HotelId,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            PreviousStatus = previousStatus.ToString(),
            NewStatus = room.Status.ToString(),
            Reason = room.OutOfOrderReason,
            OccurredOnUtc = DateTime.UtcNow
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = DateTime.UtcNow,
            Type = "room.status_changed",
            Content = JsonSerializer.Serialize(eventPayload)
        };

        _context.OutboxMessages.Add(outboxMessage);

        await _context.SaveChangesAsync(ct);

        var dto = new RoomDto
        {
            Id = room.Id,
            HotelId = room.HotelId,
            RoomTypeId = room.RoomTypeId,
            RoomTypeName = room.RoomType?.Name ?? string.Empty,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            Status = room.Status,
            OutOfOrderReason = room.OutOfOrderReason,
            CreatedAtUtc = room.CreatedAtUtc
        };

        return Result<RoomDto>.Success(dto, $"Room status updated from {previousStatus} to {room.Status}.");
    }
}
