namespace SmartHotel.HotelOps.Application.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteImageAsync(string imageUrl, CancellationToken ct = default);
}
