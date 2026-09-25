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
    private readonly string _serviceApiKey;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HotelOpsClient(
        HotelOpsGrpc.HotelOpsGrpcClient grpcClient,
        HttpClient httpClient,
        ILogger<HotelOpsClient> logger,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
    {
        _grpcClient = grpcClient;
        _httpClient = httpClient;
        _logger = logger;
        _serviceApiKey = configuration?["HOTEL_OPS_SERVICE_API_KEY"] ?? configuration?["Services:HotelOpsServiceApiKey"] ?? string.Empty;
    }

    public async Task<RoomReadinessInfo?> GetRoomReadinessAsync(Guid roomId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/rooms/{roomId}/readiness");
            if (!string.IsNullOrWhiteSpace(_serviceApiKey)) request.Headers.Add("X-Service-Api-Key", _serviceApiKey);
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) { _logger.LogWarning("Hotel Ops readiness returned {StatusCode} for room {RoomId}", response.StatusCode, roomId); return null; }
            return await response.Content.ReadFromJsonAsync<RoomReadinessInfo>(_jsonOptions, ct);
        }
        catch (Exception ex) { _logger.LogError(ex, "Hotel Ops readiness unavailable for room {RoomId}", roomId); return null; }
    }

    public async Task<RoomClaimResult> ClaimRoomAsync(Guid roomId, Guid bookingId, string source = "FrontOffice", CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/rooms/{roomId}/claim");
            if (!string.IsNullOrWhiteSpace(_serviceApiKey)) request.Headers.Add("X-Service-Api-Key", _serviceApiKey);
            request.Content = JsonContent.Create(new { BookingId = bookingId, Source = source });
            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RoomClaimResult>(_jsonOptions, ct);
                return result ?? new RoomClaimResult(true, true, roomId, bookingId, null, "Room successfully claimed.");
            }

            var errorBody = await response.Content.ReadFromJsonAsync<RoomClaimResult>(_jsonOptions, ct);
            var blocker = errorBody?.Blocker ?? errorBody?.Message ?? $"Room claim failed with status {response.StatusCode}";
            _logger.LogWarning("Hotel Ops claim failed ({StatusCode}) for room {RoomId}, booking {BookingId}: {Blocker}", response.StatusCode, roomId, bookingId, blocker);
            return new RoomClaimResult(false, false, roomId, bookingId, blocker, blocker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotel Ops claim service unavailable for room {RoomId}, booking {BookingId}", roomId, bookingId);
            return new RoomClaimResult(false, false, roomId, bookingId, "Hotel Ops readiness service is unreachable; check-in cannot proceed safely.", ex.Message);
        }
    }

    public async Task<bool> ReleaseRoomClaimAsync(Guid roomId, Guid bookingId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/rooms/{roomId}/release-claim");
            if (!string.IsNullOrWhiteSpace(_serviceApiKey)) request.Headers.Add("X-Service-Api-Key", _serviceApiKey);
            request.Content = JsonContent.Create(new { BookingId = bookingId });
            var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release Hotel Ops room claim for room {RoomId}, booking {BookingId}", roomId, bookingId);
            return false;
        }
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
                IsActive: true,
                Currency: response.Currency);
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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/rooms/internal/{roomId}");
            if (!string.IsNullOrWhiteSpace(_serviceApiKey)) request.Headers.Add("X-Service-Api-Key", _serviceApiKey);
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HotelOpsClient returned {StatusCode} for internal room {Id}", response.StatusCode, roomId);
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
