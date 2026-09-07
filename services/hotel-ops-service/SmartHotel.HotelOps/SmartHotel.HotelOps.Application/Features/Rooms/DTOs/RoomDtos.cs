using SmartHotel.HotelOps.Domain.Enums;

namespace SmartHotel.HotelOps.Application.Features.Rooms.DTOs;

public class RoomDto
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public RoomStatus Status { get; set; }
    public string? OutOfOrderReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class FloorViewDto
{
    public int Floor { get; set; }
    public int TotalRooms { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public List<RoomCategoryGroupDto> Categories { get; set; } = new();
}

public class RoomCategoryGroupDto
{
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public List<RoomDto> Rooms { get; set; } = new();
}

public class CreateRoomRequest
{
    public Guid HotelId { get; set; }
    public Guid RoomTypeId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
}

public class UpdateRoomStatusRequest
{
    public RoomStatus NewStatus { get; set; }
    public string? Reason { get; set; }
}
