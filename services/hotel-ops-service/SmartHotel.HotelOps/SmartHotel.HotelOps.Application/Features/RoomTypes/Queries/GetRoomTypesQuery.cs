using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.RoomTypes.Queries;

public record GetRoomTypesQuery(bool IsStaff = false) : IRequest<Result<List<RoomTypeDto>>>;

public class GetRoomTypesQueryHandler : IRequestHandler<GetRoomTypesQuery, Result<List<RoomTypeDto>>>
{
    private readonly IHotelOpsDbContext _context;

    public GetRoomTypesQueryHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<RoomTypeDto>>> Handle(GetRoomTypesQuery request, CancellationToken ct)
    {
        var query = _context.RoomTypes
            .Include(r => r.Images)
            .AsNoTracking();

        // If not staff/admin, filter to ONLY published room types
        if (!request.IsStaff)
        {
            query = query.Where(r => r.IsPublished && r.IsActive);
        }
        else
        {
            query = query.Where(r => r.IsActive);
        }

        var list = await query.OrderBy(r => r.PricePerNight).ToListAsync(ct);

        var dtos = list.Select(r => new RoomTypeDto
        {
            Id = r.Id,
            HotelId = r.HotelId,
            Name = r.Name,
            Title = r.Title,
            BedType = r.BedType,
            Capacity = r.Capacity,
            RoomSizeSqFt = r.RoomSizeSqFt,
            PricePerNight = r.PricePerNight,
            CleaningFee = r.CleaningFee,
            AmenitiesFee = r.AmenitiesFee,
            LongDescription = r.LongDescription,
            Highlights = r.Highlights,
            Amenities = r.Amenities,
            CancellationPolicyText = r.CancellationPolicyText,
            IsPublished = r.IsPublished,
            IsActive = r.IsActive,
            Images = r.Images.OrderBy(i => i.DisplayOrder).Select(i => new RoomTypeImageDto
            {
                Id = i.Id,
                RoomTypeId = i.RoomTypeId,
                ImageUrl = i.ImageUrl,
                DisplayOrder = i.DisplayOrder,
                IsPrimary = i.IsPrimary
            }).ToList()
        }).ToList();

        return Result<List<RoomTypeDto>>.Success(dtos);
    }
}
