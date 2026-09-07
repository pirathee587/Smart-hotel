using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.HotelOps.Grpc;

namespace SmartHotel.Booking.Infrastructure.Clients;

public class HotelOpsClient : IHotelOpsClient
{
    private readonly HotelOpsGrpc.HotelOpsGrpcClient _grpcClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HotelOpsClient> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HotelOpsClient(
        HotelOpsGrpc.HotelOpsGrpcClient grpcClient,
        HttpClient httpClient,
        ILogger<HotelOpsClient> logger)
    {
        _grpcClient = grpcClient;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RoomTypeInfo?> GetRoomTypeAsync(Guid roomTypeId, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Calling Hotel Ops gRPC GetRoomTypeAvailability for room type {Id}", roomTypeId);
            var response = await _grpcClient.GetRoomTypeAvailabilityAsync(
                new RoomTypeAvailabilityRequest { RoomTypeId = roomTypeId.ToString() },
                cancellationToken: ct);

            if (!response.Exists)
            {
                _logger.LogWarning("Hotel Ops gRPC returned Exists=false for room type {Id}", roomTypeId);
                return null;
            }

            return new RoomTypeInfo(
                roomTypeId,
                string.Empty,
                (decimal)response.PricePerNight,
                (decimal)response.CleaningFee,
                (decimal)response.AmenitiesFee,
                response.Capacity,
                response.IsPublished,
                IsActive: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reach Hotel Ops Service via gRPC for room type {Id}", roomTypeId);
            return null;
        }
    }

    public async Task<RoomInfo?> GetRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/rooms/{roomId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HotelOpsClient returned {StatusCode} for room {Id}", response.StatusCode, roomId);
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<RoomResponseDto>(_jsonOptions, ct);
            if (dto == null) return null;

            return new RoomInfo(
                dto.Id,
                dto.RoomTypeId,
                dto.RoomNumber,
                dto.Floor,
                dto.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reach Hotel Ops Service for room {Id}", roomId);
            return null;
        }
    }

    private sealed record RoomTypeResponseDto(
        Guid Id,
        string Name,
        decimal PricePerNight,
        decimal CleaningFee,
        decimal AmenitiesFee,
        int Capacity,
        bool IsPublished,
        bool IsActive);

    private sealed record RoomResponseDto(
        Guid Id,
        Guid RoomTypeId,
        string RoomNumber,
        int Floor,
        string Status);
}
