using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Images.Queries;

public record GetRoomTypeImagesQuery(Guid RoomTypeId) : IRequest<Result<List<RoomTypeImageDto>>>;

public class GetRoomTypeImagesQueryHandler : IRequestHandler<GetRoomTypeImagesQuery, Result<List<RoomTypeImageDto>>>
{
    private readonly IHotelOpsDbContext _context;

    public GetRoomTypeImagesQueryHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<RoomTypeImageDto>>> Handle(GetRoomTypeImagesQuery request, CancellationToken ct)
    {
        var roomType = await _context.RoomTypes
            .Include(r => r.Images)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomTypeId, ct);

        if (roomType == null)
        {
            return Result<List<RoomTypeImageDto>>.Failure($"Room type with ID {request.RoomTypeId} was not found.");
        }

        var images = roomType.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new RoomTypeImageDto
            {
                Id = i.Id,
                RoomTypeId = i.RoomTypeId,
                ImageUrl = i.ImageUrl,
                DisplayOrder = i.DisplayOrder,
                IsPrimary = i.IsPrimary
            })
            .ToList();

        return Result<List<RoomTypeImageDto>>.Success(images);
    }
}
