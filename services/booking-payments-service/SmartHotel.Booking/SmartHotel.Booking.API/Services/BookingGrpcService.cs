using Grpc.Core;
using MediatR;
using SmartHotel.Booking.Application.Features.Bookings.Queries;
using SmartHotel.Booking.Grpc;

namespace SmartHotel.Booking.API.Services;

public class BookingGrpcService : BookingGrpc.BookingGrpcBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BookingGrpcService> _logger;

    public BookingGrpcService(IMediator mediator, ILogger<BookingGrpcService> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public override async Task<ActiveStayResponse> GetActiveStay(
        ActiveStayRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetActiveStay received for CustomerId: {CustomerId}", request.CustomerId);

        if (!Guid.TryParse(request.CustomerId, out var customerId))
        {
            _logger.LogWarning("Invalid CustomerId format: {CustomerId}", request.CustomerId);
            return new ActiveStayResponse { HasActiveStay = false };
        }

        var result = await _mediator.Send(new GetActiveStayQuery(customerId), context.CancellationToken);

        if (!result.Succeeded || result.Data == null)
        {
            _logger.LogInformation("No active stay found for CustomerId: {CustomerId}", customerId);
            return new ActiveStayResponse { HasActiveStay = false };
        }

        var stay = result.Data;
        return new ActiveStayResponse
        {
            HasActiveStay = true,
            RoomNumber = stay.RoomNumber,
            CheckOutDate = stay.CheckOutDate.ToString("yyyy-MM-dd"),
            BookingReference = stay.BookingReference
        };
    }
}
