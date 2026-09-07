using FluentAssertions;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartHotel.Booking.API.Services;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Features.Bookings.Queries;
using SmartHotel.Booking.Grpc;
using SmartHotel.Booking.Infrastructure.Clients;
using SmartHotel.HotelOps.Grpc;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class GrpcIntegrationTests
{
    [Fact]
    public async Task HotelOpsClient_SuccessfulGrpcCall_ReturnsMappedRoomTypeInfo()
    {
        var roomTypeId = Guid.NewGuid();
        var mockGrpcClient = new Mock<HotelOpsGrpc.HotelOpsGrpcClient>();

        var expectedResponse = new RoomTypeAvailabilityResponse
        {
            Exists = true,
            IsPublished = true,
            Capacity = 3,
            PricePerNight = 25000.0,
            CleaningFee = 2000.0,
            AmenitiesFee = 1500.0
        };

        var asyncUnaryCall = new AsyncUnaryCall<RoomTypeAvailabilityResponse>(
            Task.FromResult(expectedResponse),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

        mockGrpcClient
            .Setup(c => c.GetRoomTypeAvailabilityAsync(
                It.Is<RoomTypeAvailabilityRequest>(r => r.RoomTypeId == roomTypeId.ToString()),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(asyncUnaryCall);

        var httpClient = new HttpClient();
        var client = new HotelOpsClient(mockGrpcClient.Object, httpClient, NullLogger<HotelOpsClient>.Instance);

        var result = await client.GetRoomTypeAsync(roomTypeId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(roomTypeId);
        result.Capacity.Should().Be(3);
        result.PricePerNight.Should().Be(25000.0m);
        result.CleaningFee.Should().Be(2000.0m);
        result.AmenitiesFee.Should().Be(1500.0m);
        result.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task HotelOpsClient_NonExistentRoomType_ReturnsNull()
    {
        var roomTypeId = Guid.NewGuid();
        var mockGrpcClient = new Mock<HotelOpsGrpc.HotelOpsGrpcClient>();

        var expectedResponse = new RoomTypeAvailabilityResponse
        {
            Exists = false
        };

        var asyncUnaryCall = new AsyncUnaryCall<RoomTypeAvailabilityResponse>(
            Task.FromResult(expectedResponse),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

        mockGrpcClient
            .Setup(c => c.GetRoomTypeAvailabilityAsync(
                It.IsAny<RoomTypeAvailabilityRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(asyncUnaryCall);

        var httpClient = new HttpClient();
        var client = new HotelOpsClient(mockGrpcClient.Object, httpClient, NullLogger<HotelOpsClient>.Instance);

        var result = await client.GetRoomTypeAsync(roomTypeId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task BookingGrpcService_ActiveStayFound_ReturnsActiveStayResponse()
    {
        var customerId = Guid.NewGuid();
        var mockMediator = new Mock<IMediator>();

        var activeStayDto = new ActiveStayDto
        {
            BookingId = Guid.NewGuid(),
            BookingReference = "TH-2026-GRPC",
            CustomerId = customerId,
            RoomId = Guid.NewGuid(),
            RoomNumber = "305",
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = new DateOnly(2026, 9, 6),
            CheckOutDate = new DateOnly(2026, 9, 10),
            GuestCount = 2,
            Status = Domain.Enums.BookingStatus.CheckedIn,
            IsActive = true
        };

        mockMediator
            .Setup(m => m.Send(It.Is<GetActiveStayQuery>(q => q.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ActiveStayDto>.Success(activeStayDto));

        var service = new BookingGrpcService(mockMediator.Object, NullLogger<BookingGrpcService>.Instance);

        var response = await service.GetActiveStay(
            new ActiveStayRequest { CustomerId = customerId.ToString() },
            TestServerCallContext.Create());

        response.Should().NotBeNull();
        response.HasActiveStay.Should().BeTrue();
        response.RoomNumber.Should().Be("305");
        response.CheckOutDate.Should().Be("2026-09-10");
        response.BookingReference.Should().Be("TH-2026-GRPC");
    }

    [Fact]
    public async Task BookingGrpcService_NoActiveStay_ReturnsHasActiveStayFalse()
    {
        var customerId = Guid.NewGuid();
        var mockMediator = new Mock<IMediator>();

        mockMediator
            .Setup(m => m.Send(It.IsAny<GetActiveStayQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ActiveStayDto>.Failure("No active stay found"));

        var service = new BookingGrpcService(mockMediator.Object, NullLogger<BookingGrpcService>.Instance);

        var response = await service.GetActiveStay(
            new ActiveStayRequest { CustomerId = customerId.ToString() },
            TestServerCallContext.Create());

        response.Should().NotBeNull();
        response.HasActiveStay.Should().BeFalse();
    }
}

public class TestServerCallContext : ServerCallContext
{
    public static TestServerCallContext Create() => new();

    protected override string MethodCore => "TestMethod";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "127.0.0.1";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
    protected override Metadata RequestHeadersCore => new();
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore => new();
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new("test", new Dictionary<string, List<AuthProperty>>());
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotImplementedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
