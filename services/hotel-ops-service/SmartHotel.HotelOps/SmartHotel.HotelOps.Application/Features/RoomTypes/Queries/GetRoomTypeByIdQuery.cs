using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.RoomTypes.Queries;

public record GetRoomTypeByIdQuery(Guid Id, bool IsStaff = false) : IRequest<Result<RoomTypeDto>>;

public class GetRoomTypeByIdQueryHandler : IRequestHandler<GetRoomTypeByIdQuery, Result<RoomTypeDto>>
{
    private readonly IHotelOpsDbContext _context;

    public GetRoomTypeByIdQueryHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RoomTypeDto>> Handle(GetRoomTypeByIdQuery request, CancellationToken ct)
    {
        var roomType = await _context.RoomTypes
            .Include(r => r.Images)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);

        if (roomType == null)
        {
            return Result<RoomTypeDto>.Failure($"Room type with ID {request.Id} was not found.");
        }

        if (!request.IsStaff && !roomType.IsPublished)
        {
            return Result<RoomTypeDto>.Failure($"Room type with ID {request.Id} is not published.");
        }

        var dto = new RoomTypeDto
        {
            Id = roomType.Id,
            HotelId = roomType.HotelId,
            Name = roomType.Name,
            Title = roomType.Title,
            BedType = roomType.BedType,
            Capacity = roomType.Capacity,
            RoomSizeSqFt = roomType.RoomSizeSqFt,
            PricePerNight = roomType.PricePerNight,
            CleaningFee = roomType.CleaningFee,
            AmenitiesFee = roomType.AmenitiesFee,
            LongDescription = roomType.LongDescription,
            Highlights = roomType.Highlights,
            Amenities = roomType.Amenities,
            CancellationPolicyText = roomType.CancellationPolicyText,
            IsPublished = roomType.IsPublished,
            IsActive = roomType.IsActive,
            Images = roomType.Images.OrderBy(i => i.DisplayOrder).Select(i => new RoomTypeImageDto
            {
                Id = i.Id,
                RoomTypeId = i.RoomTypeId,
                ImageUrl = i.ImageUrl,
                DisplayOrder = i.DisplayOrder,
                IsPrimary = i.IsPrimary
            }).ToList()
        };

        return Result<RoomTypeDto>.Success(dto);
    }
}
