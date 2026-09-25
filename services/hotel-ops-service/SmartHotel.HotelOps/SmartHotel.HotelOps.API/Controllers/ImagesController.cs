using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.HotelOps.Application.Features.Images.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.API.Controllers;

[ApiController]
[Route("api/v1/images")]
[Produces("application/json")]
public class ImagesController : ControllerBase
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    private readonly IImageStorageService _storageService;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        IImageStorageService storageService,
        ILogger<ImagesController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Upload an image to Supabase Storage under a target category/folder (rooms, hotels, employees).
    /// Enforces max file size of 5MB and allowed formats: JPEG, PNG, WebP.
    /// Requires Admin or Staff role.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Roles = "Admin,Staff")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadImage(
        [FromQuery] string category = "rooms",
        IFormFile? file = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file provided for upload." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "Image file exceeds the maximum allowed size of 5MB." });
        }

        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Invalid image format. Only JPEG, PNG, and WebP are allowed." });
        }

        var normalizedCategory = NormalizeCategory(category);

        using var stream = file.OpenReadStream();
        var publicUrl = await _storageService.UploadImageAsync(
            stream,
            file.FileName,
            file.ContentType,
            folder: normalizedCategory,
            ct: ct);

        var response = new ImageUploadResponse
        {
            FileName = file.FileName,
            Category = normalizedCategory,
            Url = publicUrl
        };

        return Ok(response);
    }

    /// <summary>
    /// List images for a given category (rooms, hotels, employees).
    /// Publicly accessible for 'rooms' and 'hotels'.
    /// Requires authentication when category is 'employees'.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<StorageFileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetImages(
        [FromQuery] string category = "rooms",
        CancellationToken ct = default)
    {
        var normalizedCategory = NormalizeCategory(category);

        // Require authentication if accessing employee category images
        if (string.Equals(normalizedCategory, "employees", StringComparison.OrdinalIgnoreCase))
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { message = "Authentication is required to view employee images." });
            }
        }

        var images = await _storageService.ListImagesAsync(normalizedCategory, ct);
        return Ok(images);
    }

    /// <summary>
    /// Delete an image from storage by URL or storage path.
    /// Requires Admin or Staff role.
    /// </summary>
    [HttpDelete]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteImage(
        [FromBody] DeleteImageRequest? request,
        CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return BadRequest(new { message = "ImageUrl is required in the request body." });
        }

        await _storageService.DeleteImageAsync(request.ImageUrl, ct);
        return Ok(new { message = "Image deleted successfully.", imageUrl = request.ImageUrl });
    }

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "rooms";
        }

        var lower = category.Trim().ToLowerInvariant();
        return lower switch
        {
            "rooms" or "room" => "rooms",
            "hotels" or "hotel" => "hotels",
            "employees" or "employee" => "employees",
            _ => lower
        };
    }
}

public class ImageUploadResponse
{
    public string FileName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class DeleteImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
}
