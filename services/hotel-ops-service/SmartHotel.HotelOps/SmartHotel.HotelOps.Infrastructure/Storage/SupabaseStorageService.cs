using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartHotel.HotelOps.Application.Features.Images.DTOs;
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
    private readonly bool _isProduction;

    public SupabaseStorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _supabaseUrl = _configuration["Supabase:Url"] ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
        _supabaseKey = _configuration["Supabase:Key"] ??
                       _configuration["Supabase:ApiKey"] ??
                       Environment.GetEnvironmentVariable("SUPABASE_KEY") ??
                       Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
        _bucketName = _configuration["Supabase:Bucket"] ??
                      _configuration["Supabase:BucketName"] ??
                      throw new InvalidOperationException("Supabase storage bucket name is not configured in appsettings.json (Supabase:Bucket).");

        _localStoragePath = _configuration["HOTELOPS_STORAGE_LOCAL_PATH"] ??
                            Path.Combine(AppContext.BaseDirectory, "uploads", _bucketName);

        var environmentName = _configuration["ASPNETCORE_ENVIRONMENT"] ??
                              Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> UploadImageAsync(
        Stream stream,
        string fileName,
        string contentType,
        string folder = "rooms",
        CancellationToken ct = default)
    {
        var cleanFolder = SanitizeFolder(folder);
        var extension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var storagePath = $"{cleanFolder}/{uniqueFileName}";

        // Attempt upload via Supabase Storage REST API if credentials exist
        if (!string.IsNullOrWhiteSpace(_supabaseUrl) && !string.IsNullOrWhiteSpace(_supabaseKey))
        {
            try
            {
                var uploadUrl = $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/{_bucketName}/{storagePath}";
                using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
                request.Headers.Add("apikey", _supabaseKey);

                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                request.Content = content;

                var response = await _httpClient.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    var publicUrl = GetPublicUrl(storagePath);
                    _logger.LogInformation("Image uploaded successfully to Supabase Storage: {PublicUrl}", publicUrl);
                    return publicUrl;
                }

                if (_isProduction)
                {
                    _logger.LogWarning(
                        "Supabase storage upload failed with status {StatusCode} in PRODUCTION environment. Falling back to local disk storage.",
                        response.StatusCode);
                }
                else
                {
                    _logger.LogInformation(
                        "Supabase storage upload returned status {StatusCode}. Falling back to local disk storage.",
                        response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                if (_isProduction)
                {
                    _logger.LogWarning(
                        ex,
                        "Error uploading image to Supabase Storage in PRODUCTION environment. Falling back to local disk storage.");
                }
                else
                {
                    _logger.LogInformation(
                        ex,
                        "Error uploading to Supabase Storage. Falling back to local disk storage.");
                }
            }
        }
        else if (_isProduction)
        {
            _logger.LogWarning(
                "Supabase URL or Key is not configured in PRODUCTION environment. Falling back to local disk storage.");
        }

        // Local disk fallback
        var folderDiskPath = Path.Combine(_localStoragePath, cleanFolder);
        if (!Directory.Exists(folderDiskPath))
        {
            Directory.CreateDirectory(folderDiskPath);
        }

        var localFilePath = Path.Combine(folderDiskPath, uniqueFileName);
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(fileStream, ct);
        }

        var localUrl = $"/uploads/{_bucketName}/{cleanFolder}/{uniqueFileName}";
        _logger.LogInformation("Image stored locally on disk fallback: {LocalFilePath}", localFilePath);
        return localUrl;
    }

    public async Task<IEnumerable<StorageFileDto>> ListImagesAsync(string folder = "rooms", CancellationToken ct = default)
    {
        var cleanFolder = SanitizeFolder(folder);
        var resultList = new List<StorageFileDto>();

        // Attempt listing via Supabase Storage REST API
        if (!string.IsNullOrWhiteSpace(_supabaseUrl) && !string.IsNullOrWhiteSpace(_supabaseKey))
        {
            try
            {
                var listUrl = $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/list/{_bucketName}";
                using var request = new HttpRequestMessage(HttpMethod.Post, listUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
                request.Headers.Add("apikey", _supabaseKey);

                var payload = new
                {
                    prefix = cleanFolder,
                    limit = 100,
                    offset = 0,
                    sortBy = new { column = "name", order = "asc" }
                };

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    using var document = JsonDocument.Parse(responseJson);

                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in document.RootElement.EnumerateArray())
                        {
                            var name = element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                            if (string.IsNullOrWhiteSpace(name) || name.StartsWith(".emptyFolderPlaceholder"))
                            {
                                continue;
                            }

                            var path = $"{cleanFolder}/{name}";
                            var url = GetPublicUrl(path);

                            long? size = null;
                            string? mime = null;
                            if (element.TryGetProperty("metadata", out var metaProp) && metaProp.ValueKind == JsonValueKind.Object)
                            {
                                if (metaProp.TryGetProperty("size", out var s) && s.TryGetInt64(out var sizeVal))
                                    size = sizeVal;
                                if (metaProp.TryGetProperty("mimetype", out var m))
                                    mime = m.GetString();
                            }

                            DateTimeOffset? createdAt = null;
                            if (element.TryGetProperty("created_at", out var createdProp) &&
                                DateTimeOffset.TryParse(createdProp.GetString(), out var parsedDate))
                            {
                                createdAt = parsedDate;
                            }

                            resultList.Add(new StorageFileDto
                            {
                                Name = name,
                                Path = path,
                                Url = url,
                                SizeBytes = size,
                                ContentType = mime,
                                CreatedAt = createdAt
                            });
                        }

                        return resultList;
                    }
                }
                else if (_isProduction)
                {
                    _logger.LogWarning(
                        "Supabase storage listing failed with status {StatusCode} in PRODUCTION environment.",
                        response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                if (_isProduction)
                {
                    _logger.LogWarning(
                        ex,
                        "Error listing images from Supabase Storage in PRODUCTION environment.");
                }
                else
                {
                    _logger.LogInformation(
                        ex,
                        "Error listing images from Supabase Storage. Checking local disk fallback.");
                }
            }
        }

        // Local disk fallback list
        var folderDiskPath = Path.Combine(_localStoragePath, cleanFolder);
        if (Directory.Exists(folderDiskPath))
        {
            var files = Directory.GetFiles(folderDiskPath);
            foreach (var filePath in files)
            {
                var fileInfo = new FileInfo(filePath);
                var path = $"{cleanFolder}/{fileInfo.Name}";
                resultList.Add(new StorageFileDto
                {
                    Name = fileInfo.Name,
                    Path = path,
                    Url = $"/uploads/{_bucketName}/{cleanFolder}/{fileInfo.Name}",
                    SizeBytes = fileInfo.Length,
                    ContentType = GetMimeType(fileInfo.Extension),
                    CreatedAt = fileInfo.CreationTimeUtc
                });
            }
        }

        return resultList;
    }

    public async Task DeleteImageAsync(string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        var relativePath = ExtractRelativeStoragePath(imageUrl);

        if (!string.IsNullOrWhiteSpace(_supabaseUrl) &&
            !string.IsNullOrWhiteSpace(_supabaseKey) &&
            (imageUrl.Contains("supabase") || !imageUrl.StartsWith("/uploads/")))
        {
            try
            {
                var deleteUrl = $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/{_bucketName}/{relativePath}";
                using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
                request.Headers.Add("apikey", _supabaseKey);

                var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode && _isProduction)
                {
                    _logger.LogWarning(
                        "Failed to delete image {ImageUrl} from Supabase Storage. Status: {StatusCode}",
                        imageUrl,
                        response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image from Supabase storage: {ImageUrl}", imageUrl);
            }
        }

        // Clean up local disk file if present
        var localDiskPath = Path.Combine(_localStoragePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localDiskPath))
        {
            try
            {
                File.Delete(localDiskPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete local fallback image file: {LocalDiskPath}", localDiskPath);
            }
        }
    }

    public string GetPublicUrl(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return string.Empty;
        }

        if (storagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return storagePath;
        }

        if (storagePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return storagePath;
        }

        var cleanPath = storagePath.TrimStart('/');

        if (!string.IsNullOrWhiteSpace(_supabaseUrl))
        {
            return $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{_bucketName}/{cleanPath}";
        }

        return $"/uploads/{_bucketName}/{cleanPath}";
    }

    private static string SanitizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return "rooms";
        }

        var cleaned = folder.Trim().Trim('/', '\\').Replace('\\', '/');
        return string.IsNullOrWhiteSpace(cleaned) ? "rooms" : cleaned;
    }

    private string ExtractRelativeStoragePath(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        // Check for Supabase public object URL: .../object/public/{bucketName}/{path}
        var publicToken = $"/object/public/{_bucketName}/";
        var idx = imageUrl.IndexOf(publicToken, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return imageUrl.Substring(idx + publicToken.Length);
        }

        // Check for local uploads URL: /uploads/{bucketName}/{path}
        var localToken = $"/uploads/{_bucketName}/";
        var localIdx = imageUrl.IndexOf(localToken, StringComparison.OrdinalIgnoreCase);
        if (localIdx >= 0)
        {
            return imageUrl.Substring(localIdx + localToken.Length);
        }

        // Fallback: return file name or trimmed path
        return Path.GetFileName(imageUrl);
    }

    private static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
