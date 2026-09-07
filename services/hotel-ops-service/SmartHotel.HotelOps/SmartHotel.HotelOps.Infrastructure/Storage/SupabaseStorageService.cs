using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Infrastructure.Storage;

public class SupabaseStorageService : IImageStorageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SupabaseStorageService> _logger;

    private readonly string? _supabaseUrl;
    private readonly string? _supabaseKey;
    private readonly string _bucketName;
    private readonly string _localStoragePath;

    public SupabaseStorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _supabaseUrl = _configuration["Supabase:Url"] ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
        _supabaseKey = _configuration["Supabase:Key"] ?? Environment.GetEnvironmentVariable("SUPABASE_KEY");
        _bucketName = _configuration["Supabase:Bucket"] ?? "room-images";

        _localStoragePath = _configuration["HOTELOPS_STORAGE_LOCAL_PATH"] ??
                            Path.Combine(AppContext.BaseDirectory, "uploads", "room-images");
    }

    public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";

        // If Supabase credentials are provided, upload via Supabase Storage REST API
        if (!string.IsNullOrEmpty(_supabaseUrl) && !string.IsNullOrEmpty(_supabaseKey))
        {
            try
            {
                var uploadUrl = $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/{_bucketName}/{uniqueFileName}";
                using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
                request.Headers.Add("apikey", _supabaseKey);

                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                request.Content = content;

                var response = await _httpClient.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    var publicUrl = $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{_bucketName}/{uniqueFileName}";
                    _logger.LogInformation("Image uploaded to Supabase Storage: {PublicUrl}", publicUrl);
                    return publicUrl;
                }

                _logger.LogWarning("Supabase upload returned status {StatusCode}. Falling back to local storage.", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error uploading to Supabase Storage. Falling back to local disk storage.");
            }
        }

        // Fallback: Local file storage
        if (!Directory.Exists(_localStoragePath))
        {
            Directory.CreateDirectory(_localStoragePath);
        }

        var localFilePath = Path.Combine(_localStoragePath, uniqueFileName);
        stream.Seek(0, SeekOrigin.Begin);
        using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(fileStream, ct);
        }

        var localUrl = $"/uploads/room-images/{uniqueFileName}";
        _logger.LogInformation("Image stored locally: {LocalFilePath}", localFilePath);
        return localUrl;
    }

    public async Task DeleteImageAsync(string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            return;
        }

        var fileName = Path.GetFileName(imageUrl);

        if (!string.IsNullOrEmpty(_supabaseUrl) && !string.IsNullOrEmpty(_supabaseKey) && imageUrl.Contains("supabase"))
        {
            try
            {
                var deleteUrl = $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/{_bucketName}/{fileName}";
                using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
                request.Headers.Add("apikey", _supabaseKey);

                await _httpClient.SendAsync(request, ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image from Supabase storage: {ImageUrl}", imageUrl);
            }
        }

        var localFilePath = Path.Combine(_localStoragePath, fileName);
        if (File.Exists(localFilePath))
        {
            try
            {
                File.Delete(localFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete local image file: {LocalFilePath}", localFilePath);
            }
        }
    }
}
