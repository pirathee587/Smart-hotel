using Grpc.Core;
using MediatR;
using SmartHotel.HotelOps.Application.Features.RoomTypes.Queries;
using SmartHotel.HotelOps.Grpc;

namespace SmartHotel.HotelOps.API.Services;

public class HotelOpsGrpcService : HotelOpsGrpc.HotelOpsGrpcBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<HotelOpsGrpcService> _logger;

    public HotelOpsGrpcService(IMediator mediator, ILogger<HotelOpsGrpcService> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public override async Task<RoomTypeAvailabilityResponse> GetRoomTypeAvailability(
        RoomTypeAvailabilityRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetRoomTypeAvailability received for RoomTypeId: {RoomTypeId}", request.RoomTypeId);

        if (!Guid.TryParse(request.RoomTypeId, out var roomTypeId))
        {
            _logger.LogWarning("Invalid GUID provided for RoomTypeId: {RoomTypeId}", request.RoomTypeId);
            return new RoomTypeAvailabilityResponse { Exists = false };
        }

        var result = await _mediator.Send(new GetRoomTypeByIdQuery(roomTypeId, IsStaff: true), context.CancellationToken);

        if (!result.Succeeded || result.Data == null)
        {
            _logger.LogWarning("Room type not found in HotelOps: {RoomTypeId}", roomTypeId);
            return new RoomTypeAvailabilityResponse { Exists = false };
        }

        var data = result.Data;
        return new RoomTypeAvailabilityResponse
        {
            Exists = true,
            IsPublished = data.IsPublished,
            Capacity = data.Capacity,
            PricePerNight = (double)data.PricePerNight,
            CleaningFee = (double)data.CleaningFee,
            AmenitiesFee = (double)data.AmenitiesFee
        };
    }
}
