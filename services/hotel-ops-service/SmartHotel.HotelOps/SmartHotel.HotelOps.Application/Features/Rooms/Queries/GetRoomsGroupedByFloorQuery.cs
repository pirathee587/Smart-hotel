using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Rooms.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Rooms.Queries;

public record GetRoomsGroupedByFloorQuery(Guid? HotelId = null) : IRequest<Result<List<FloorViewDto>>>;

public class GetRoomsGroupedByFloorQueryHandler : IRequestHandler<GetRoomsGroupedByFloorQuery, Result<List<FloorViewDto>>>
{
    private readonly IHotelOpsDbContext _context;

    public GetRoomsGroupedByFloorQueryHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<FloorViewDto>>> Handle(GetRoomsGroupedByFloorQuery request, CancellationToken ct)
    {
        var query = _context.Rooms
            .Include(r => r.RoomType)
            .AsNoTracking();

        if (request.HotelId.HasValue)
        {
            query = query.Where(r => r.HotelId == request.HotelId.Value);
        }

        var allRooms = await query.ToListAsync(ct);

        var floorGroups = allRooms
            .GroupBy(r => r.Floor)
            .OrderBy(g => g.Key)
            .Select(floorGroup =>
            {
                var floorDto = new FloorViewDto
                {
                    Floor = floorGroup.Key,
                    TotalRooms = floorGroup.Count(),
                    StatusCounts = floorGroup
                        .GroupBy(r => r.Status.ToString())
                        .ToDictionary(sg => sg.Key, sg => sg.Count()),
                    Categories = floorGroup
                        .GroupBy(r => new { r.RoomTypeId, Name = r.RoomType?.Name ?? "Unknown" })
                        .OrderBy(cg => cg.Key.Name)
                        .Select(catGroup => new RoomCategoryGroupDto
                        {
                            RoomTypeId = catGroup.Key.RoomTypeId,
                            RoomTypeName = catGroup.Key.Name,
                            Rooms = catGroup.OrderBy(r => r.RoomNumber).Select(r => new RoomDto
                            {
                                Id = r.Id,
                                HotelId = r.HotelId,
                                RoomTypeId = r.RoomTypeId,
                                RoomTypeName = catGroup.Key.Name,
                                RoomNumber = r.RoomNumber,
                                Floor = r.Floor,
                                Status = r.Status,
                                OutOfOrderReason = r.OutOfOrderReason,
                                CreatedAtUtc = r.CreatedAtUtc
                            }).ToList()
                        }).ToList()
                };
                return floorDto;
            })
            .ToList();

        return Result<List<FloorViewDto>>.Success(floorGroups);
    }
}
