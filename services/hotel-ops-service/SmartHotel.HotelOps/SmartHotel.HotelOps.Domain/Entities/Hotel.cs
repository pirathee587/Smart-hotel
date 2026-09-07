using SmartHotel.HotelOps.Domain.Common;

namespace SmartHotel.HotelOps.Domain.Entities;

public class Hotel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalFloors { get; set; }
    public int TotalRooms { get; set; }

    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<RoomType> RoomTypes { get; set; } = new List<RoomType>();
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
