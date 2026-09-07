using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Rooms.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;

namespace SmartHotel.HotelOps.Application.Features.Rooms.Commands;

public record CreateRoomCommand(CreateRoomRequest Request) : IRequest<Result<RoomDto>>;

public class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, Result<RoomDto>>
{
    private readonly IHotelOpsDbContext _context;

    public CreateRoomCommandHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RoomDto>> Handle(CreateRoomCommand command, CancellationToken ct)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.RoomNumber))
        {
            return Result<RoomDto>.Failure("Room number is required.");
        }

        var roomTypeExists = await _context.RoomTypes.AnyAsync(rt => rt.Id == req.RoomTypeId, ct);
        if (!roomTypeExists)
        {
            return Result<RoomDto>.Failure($"Room type with ID {req.RoomTypeId} does not exist.");
        }

        var duplicate = await _context.Rooms.AnyAsync(
            r => r.HotelId == req.HotelId && r.RoomNumber.ToLower() == req.RoomNumber.Trim().ToLower(),
            ct);

        if (duplicate)
        {
            return Result<RoomDto>.Failure($"Room number '{req.RoomNumber}' already exists in this hotel.");
        }

        var room = new Room
        {
            HotelId = req.HotelId,
            RoomTypeId = req.RoomTypeId,
            RoomNumber = req.RoomNumber.Trim(),
            Floor = req.Floor,
            Status = RoomStatus.Available
        };

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(ct);

        var roomType = await _context.RoomTypes.FirstAsync(rt => rt.Id == room.RoomTypeId, ct);

        var dto = new RoomDto
        {
            Id = room.Id,
            HotelId = room.HotelId,
            RoomTypeId = room.RoomTypeId,
            RoomTypeName = roomType.Name,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            Status = room.Status,
            OutOfOrderReason = room.OutOfOrderReason,
            CreatedAtUtc = room.CreatedAtUtc
        };

        return Result<RoomDto>.Success(dto, "Room created successfully.");
    }
}
