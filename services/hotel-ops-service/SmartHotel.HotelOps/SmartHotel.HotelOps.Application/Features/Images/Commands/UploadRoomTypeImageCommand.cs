using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;

namespace SmartHotel.HotelOps.Application.Features.Images.Commands;

public record UploadRoomTypeImageCommand(
    Guid RoomTypeId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    bool IsPrimary = false) : IRequest<Result<RoomTypeImageDto>>;

public class UploadRoomTypeImageCommandHandler : IRequestHandler<UploadRoomTypeImageCommand, Result<RoomTypeImageDto>>
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    private readonly IHotelOpsDbContext _context;
    private readonly IImageStorageService _storageService;

    public UploadRoomTypeImageCommandHandler(IHotelOpsDbContext context, IImageStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<Result<RoomTypeImageDto>> Handle(UploadRoomTypeImageCommand command, CancellationToken ct)
    {
        var roomType = await _context.RoomTypes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == command.RoomTypeId, ct);

        if (roomType == null)
        {
            return Result<RoomTypeImageDto>.Failure($"Room type with ID {command.RoomTypeId} was not found.");
        }

        if (command.FileSizeBytes > MaxFileSizeBytes)
        {
            return Result<RoomTypeImageDto>.Failure("Image file exceeds the maximum allowed size of 5MB.");
        }

        if (!AllowedMimeTypes.Contains(command.ContentType))
        {
            return Result<RoomTypeImageDto>.Failure("Invalid image format. Only JPEG, PNG, and WebP are allowed.");
        }

        var imageUrl = await _storageService.UploadImageAsync(
            command.FileStream,
            command.FileName,
            command.ContentType,
            ct);

        var existingImagesCount = roomType.Images.Count;
        var isPrimary = command.IsPrimary || existingImagesCount == 0;

        if (isPrimary && existingImagesCount > 0)
        {
            foreach (var img in roomType.Images)
            {
                img.IsPrimary = false;
            }
        }

        var newImage = new RoomTypeImage
        {
            RoomTypeId = roomType.Id,
            ImageUrl = imageUrl,
            DisplayOrder = existingImagesCount + 1,
            IsPrimary = isPrimary
        };

        _context.RoomTypeImages.Add(newImage);
        await _context.SaveChangesAsync(ct);

        var dto = new RoomTypeImageDto
        {
            Id = newImage.Id,
            RoomTypeId = newImage.RoomTypeId,
            ImageUrl = newImage.ImageUrl,
            DisplayOrder = newImage.DisplayOrder,
            IsPrimary = newImage.IsPrimary
        };

        return Result<RoomTypeImageDto>.Success(dto, "Image uploaded and attached successfully.");
    }
}
