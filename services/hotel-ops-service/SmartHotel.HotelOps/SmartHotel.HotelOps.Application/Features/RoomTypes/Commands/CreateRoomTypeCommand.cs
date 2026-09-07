using MediatR;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;

namespace SmartHotel.HotelOps.Application.Features.RoomTypes.Commands;

public record CreateRoomTypeCommand(CreateRoomTypeRequest Request) : IRequest<Result<RoomTypeDto>>;

public class CreateRoomTypeCommandHandler : IRequestHandler<CreateRoomTypeCommand, Result<RoomTypeDto>>
{
    private readonly IHotelOpsDbContext _context;

    public CreateRoomTypeCommandHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RoomTypeDto>> Handle(CreateRoomTypeCommand command, CancellationToken ct)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Result<RoomTypeDto>.Failure("Room type name is required.");
        }

        if (req.PricePerNight <= 0)
        {
            return Result<RoomTypeDto>.Failure("Price per night must be greater than zero.");
        }

        var roomType = new RoomType
        {
            HotelId = req.HotelId,
            Name = req.Name.Trim(),
            Title = req.Title.Trim(),
            BedType = req.BedType.Trim(),
            Capacity = req.Capacity,
            RoomSizeSqFt = req.RoomSizeSqFt,
            PricePerNight = req.PricePerNight,
            CleaningFee = req.CleaningFee,
            AmenitiesFee = req.AmenitiesFee,
            LongDescription = req.LongDescription.Trim(),
            Highlights = req.Highlights,
            Amenities = req.Amenities,
            CancellationPolicyText = req.CancellationPolicyText.Trim(),
            IsPublished = false, // Draft by default until Admin publishes
            IsActive = true
        };

        _context.RoomTypes.Add(roomType);
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
            IsActive = roomType.IsActive
        };

        return Result<RoomTypeDto>.Success(dto, "Draft room type created successfully.");
    }
}
