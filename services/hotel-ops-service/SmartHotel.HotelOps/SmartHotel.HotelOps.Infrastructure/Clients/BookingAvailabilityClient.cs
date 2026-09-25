using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Infrastructure.Clients;

/// <summary>
/// HTTP client that calls the booking-payments-service availability endpoint
/// to determine how many rooms of a type are still bookable for given dates.
/// </summary>
public class BookingAvailabilityClient : IBookingAvailabilityClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BookingAvailabilityClient> _logger;

    public BookingAvailabilityClient(HttpClient httpClient, IConfiguration configuration, ILogger<BookingAvailabilityClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Base address can be overridden via config; falls back to docker-compose default
        var baseUrl = configuration["Services:BookingServiceUrl"] ?? "http://booking-payments-service:5003";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(8);
    }

    public async Task<RoomAvailabilityResult> GetAvailabilityAsync(
        Guid roomTypeId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/v1/bookings/availability?roomTypeId={roomTypeId}" +
                      $"&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}";

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Availability check returned {Status} for RoomType {RoomTypeId}",
                    response.StatusCode, roomTypeId);
                // Fail open — treat as unknown, let hotel-ops compute based on room count
                return new RoomAvailabilityResult(roomTypeId, 0, 0, null);
            }

            var dto = await response.Content.ReadFromJsonAsync<BookingAvailabilityDto>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            if (dto is null)
                return new RoomAvailabilityResult(roomTypeId, 0, 0, null);

            return new RoomAvailabilityResult(
                roomTypeId,
                TotalRooms: dto.TotalRooms,
                AvailableCount: dto.AvailableCount,
                FirstAvailableRoomId: dto.FirstAvailableRoomId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Booking service unavailable during availability check for RoomType {RoomTypeId}", roomTypeId);
            return new RoomAvailabilityResult(roomTypeId, 0, 0, null);
        }
    }

    private sealed record BookingAvailabilityDto(
        Guid RoomTypeId,
        int TotalRooms,
        int AvailableCount,
        Guid? FirstAvailableRoomId);
}
