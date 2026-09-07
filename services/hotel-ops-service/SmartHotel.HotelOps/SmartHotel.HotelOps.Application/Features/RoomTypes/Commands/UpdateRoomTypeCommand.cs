using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.RoomTypes.Commands;

public record UpdateRoomTypeCommand(Guid Id, UpdateRoomTypeRequest Request) : IRequest<Result<RoomTypeDto>>;

public class UpdateRoomTypeCommandHandler : IRequestHandler<UpdateRoomTypeCommand, Result<RoomTypeDto>>
{
    private readonly IHotelOpsDbContext _context;

    public UpdateRoomTypeCommandHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RoomTypeDto>> Handle(UpdateRoomTypeCommand command, CancellationToken ct)
    {
        var roomType = await _context.RoomTypes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == command.Id, ct);

        if (roomType == null)
        {
            return Result<RoomTypeDto>.Failure($"Room type with ID {command.Id} was not found.");
        }

        var req = command.Request;
        roomType.Name = req.Name.Trim();
        roomType.Title = req.Title.Trim();
        roomType.BedType = req.BedType.Trim();
        roomType.Capacity = req.Capacity;
        roomType.RoomSizeSqFt = req.RoomSizeSqFt;
        roomType.PricePerNight = req.PricePerNight;
        roomType.CleaningFee = req.CleaningFee;
        roomType.AmenitiesFee = req.AmenitiesFee;
        roomType.LongDescription = req.LongDescription.Trim();
        roomType.Highlights = req.Highlights;
        roomType.Amenities = req.Amenities;
        roomType.CancellationPolicyText = req.CancellationPolicyText.Trim();
        roomType.IsActive = req.IsActive;
        roomType.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

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

        return Result<RoomTypeDto>.Success(dto, "Room type updated successfully.");
    }
}
