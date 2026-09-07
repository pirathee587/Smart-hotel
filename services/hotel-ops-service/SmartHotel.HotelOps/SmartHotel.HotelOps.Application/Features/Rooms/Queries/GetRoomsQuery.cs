using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Rooms.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Rooms.Queries;

public record GetRoomsQuery(Guid? HotelId = null) : IRequest<Result<List<RoomDto>>>;

public class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, Result<List<RoomDto>>>
{
    private readonly IHotelOpsDbContext _context;

    public GetRoomsQueryHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<RoomDto>>> Handle(GetRoomsQuery request, CancellationToken ct)
    {
        var query = _context.Rooms
            .Include(r => r.RoomType)
            .AsNoTracking();

        if (request.HotelId.HasValue && request.HotelId.Value != Guid.Empty)
        {
            query = query.Where(r => r.HotelId == request.HotelId.Value);
        }

        var list = await query
            .OrderBy(r => r.Floor)
            .ThenBy(r => r.RoomNumber)
            .ToListAsync(ct);

        var dtos = list.Select(room => new RoomDto
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
        }).ToList();

        return Result<List<RoomDto>>.Success(dtos);
    }
}
