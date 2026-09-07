using SmartHotel.HotelOps.Domain.Common;

namespace SmartHotel.HotelOps.Domain.Entities;

public class Department : BaseEntity
{
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }

    public Hotel? Hotel { get; set; }
}
