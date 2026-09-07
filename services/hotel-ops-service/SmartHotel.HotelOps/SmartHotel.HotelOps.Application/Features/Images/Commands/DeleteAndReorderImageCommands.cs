using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Images.Commands;

public record DeleteRoomTypeImageCommand(Guid RoomTypeId, Guid ImageId) : IRequest<Result>;

public class DeleteRoomTypeImageCommandHandler : IRequestHandler<DeleteRoomTypeImageCommand, Result>
{
    private readonly IHotelOpsDbContext _context;
    private readonly IImageStorageService _storageService;

    public DeleteRoomTypeImageCommandHandler(IHotelOpsDbContext context, IImageStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<Result> Handle(DeleteRoomTypeImageCommand command, CancellationToken ct)
    {
        var image = await _context.RoomTypeImages
            .FirstOrDefaultAsync(i => i.Id == command.ImageId && i.RoomTypeId == command.RoomTypeId, ct);

        if (image == null)
        {
            return Result.Failure($"Image with ID {command.ImageId} was not found for this room type.");
        }

        await _storageService.DeleteImageAsync(image.ImageUrl, ct);
        _context.RoomTypeImages.Remove(image);
        await _context.SaveChangesAsync(ct);

        return Result.Success("Image deleted successfully.");
    }
}

public record ReorderRoomTypeImagesCommand(Guid RoomTypeId, List<Guid> OrderedImageIds) : IRequest<Result>;

public class ReorderRoomTypeImagesCommandHandler : IRequestHandler<ReorderRoomTypeImagesCommand, Result>
{
    private readonly IHotelOpsDbContext _context;

    public ReorderRoomTypeImagesCommandHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ReorderRoomTypeImagesCommand command, CancellationToken ct)
    {
        var images = await _context.RoomTypeImages
            .Where(i => i.RoomTypeId == command.RoomTypeId)
            .ToListAsync(ct);

        for (int i = 0; i < command.OrderedImageIds.Count; i++)
        {
            var imgId = command.OrderedImageIds[i];
            var img = images.FirstOrDefault(x => x.Id == imgId);
            if (img != null)
            {
                img.DisplayOrder = i + 1;
                img.IsPrimary = (i == 0);
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success("Images reordered successfully.");
    }
}
