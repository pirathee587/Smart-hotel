using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.RoomTypes.Commands;

public record PublishRoomTypeCommand(Guid Id) : IRequest<Result>;

public class PublishRoomTypeCommandHandler : IRequestHandler<PublishRoomTypeCommand, Result>
{
    private readonly IHotelOpsDbContext _context;

    public PublishRoomTypeCommandHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(PublishRoomTypeCommand request, CancellationToken ct)
    {
        var roomType = await _context.RoomTypes.FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (roomType == null)
        {
            return Result.Failure($"Room type with ID {request.Id} was not found.");
        }

        roomType.IsPublished = true;
        roomType.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return Result.Success($"Room type '{roomType.Name}' published successfully.");
    }
}
