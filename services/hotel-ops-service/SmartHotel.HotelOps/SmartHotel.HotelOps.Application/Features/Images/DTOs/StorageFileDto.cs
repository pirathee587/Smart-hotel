namespace SmartHotel.HotelOps.Application.Features.Images.DTOs;

public class StorageFileDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long? SizeBytes { get; set; }
    public string? ContentType { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}
