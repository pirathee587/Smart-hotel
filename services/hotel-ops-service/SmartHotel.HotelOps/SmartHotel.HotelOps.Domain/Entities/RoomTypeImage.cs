using SmartHotel.HotelOps.Domain.Common;

namespace SmartHotel.HotelOps.Domain.Entities;

public class RoomTypeImage : BaseEntity
{
    public Guid RoomTypeId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }

    public RoomType? RoomType { get; set; }
}
