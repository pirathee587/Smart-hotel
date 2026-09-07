using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Rooms.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Rooms.Queries;

public record GetRoomByIdQuery(Guid Id) : IRequest<Result<RoomDto>>;

public class GetRoomByIdQueryHandler : IRequestHandler<GetRoomByIdQuery, Result<RoomDto>>
{
    private readonly IHotelOpsDbContext _context;

    public GetRoomByIdQueryHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RoomDto>> Handle(GetRoomByIdQuery request, CancellationToken ct)
    {
        var room = await _context.Rooms
            .Include(r => r.RoomType)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);

        if (room == null)
        {
            return Result<RoomDto>.Failure($"Room with ID {request.Id} was not found.");
        }

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

        return Result<RoomDto>.Success(dto);
    }
}
