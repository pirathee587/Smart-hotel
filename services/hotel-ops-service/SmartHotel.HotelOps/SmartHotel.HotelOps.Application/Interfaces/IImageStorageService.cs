using SmartHotel.HotelOps.Application.Features.Images.DTOs;

namespace SmartHotel.HotelOps.Application.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, string folder = "rooms", CancellationToken ct = default);
    Task<IEnumerable<StorageFileDto>> ListImagesAsync(string folder = "rooms", CancellationToken ct = default);
    Task DeleteImageAsync(string imageUrl, CancellationToken ct = default);
    string GetPublicUrl(string storagePath);
}

