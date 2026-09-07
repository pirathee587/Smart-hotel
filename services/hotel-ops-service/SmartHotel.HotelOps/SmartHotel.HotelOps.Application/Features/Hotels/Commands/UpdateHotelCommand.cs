using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Hotels.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Hotels.Commands;

public record UpdateHotelCommand(Guid Id, UpdateHotelRequest Request) : IRequest<Result<HotelDto>>;

public class UpdateHotelCommandHandler : IRequestHandler<UpdateHotelCommand, Result<HotelDto>>
{
    private readonly IHotelOpsDbContext _context;

    public UpdateHotelCommandHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<HotelDto>> Handle(UpdateHotelCommand command, CancellationToken ct)
    {
        var hotel = await _context.Hotels
            .Include(h => h.Rooms)
            .FirstOrDefaultAsync(h => h.Id == command.Id, ct);

        if (hotel == null)
        {
            return Result<HotelDto>.Failure($"Hotel with ID {command.Id} was not found.");
        }

        var req = command.Request;
        hotel.Name = req.Name.Trim();
        hotel.Address = req.Address.Trim();
        hotel.Phone = req.Phone.Trim();
        hotel.Email = req.Email.Trim();
        hotel.TotalFloors = req.TotalFloors;
        hotel.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        var dto = new HotelDto
        {
            Id = hotel.Id,
            Name = hotel.Name,
            Address = hotel.Address,
            Phone = hotel.Phone,
            Email = hotel.Email,
            TotalFloors = hotel.TotalFloors,
            TotalRooms = hotel.Rooms.Count
        };

        return Result<HotelDto>.Success(dto, "Hotel updated successfully.");
    }
}
