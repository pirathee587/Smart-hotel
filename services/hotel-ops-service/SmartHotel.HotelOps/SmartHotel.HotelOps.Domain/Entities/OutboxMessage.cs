namespace SmartHotel.HotelOps.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
}
