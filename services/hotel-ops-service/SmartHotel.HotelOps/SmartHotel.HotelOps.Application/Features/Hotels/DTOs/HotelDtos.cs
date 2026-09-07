namespace SmartHotel.HotelOps.Application.Features.Hotels.DTOs;

public class HotelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalFloors { get; set; }
    public int TotalRooms { get; set; }
}

public class UpdateHotelRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalFloors { get; set; }
}
