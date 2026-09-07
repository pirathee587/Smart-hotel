using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Hotels.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Hotels.Queries;

public record GetHotelsQuery : IRequest<Result<List<HotelDto>>>;

public class GetHotelsQueryHandler : IRequestHandler<GetHotelsQuery, Result<List<HotelDto>>>
{
    private readonly IHotelOpsDbContext _context;

    public GetHotelsQueryHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<HotelDto>>> Handle(GetHotelsQuery request, CancellationToken ct)
    {
        var list = await _context.Hotels
            .Include(h => h.Rooms)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = list.Select(h => new HotelDto
        {
            Id = h.Id,
            Name = h.Name,
            Address = h.Address,
            Phone = h.Phone,
            Email = h.Email,
            TotalFloors = h.TotalFloors,
            TotalRooms = h.Rooms.Count
        }).ToList();

        return Result<List<HotelDto>>.Success(dtos);
    }
}
