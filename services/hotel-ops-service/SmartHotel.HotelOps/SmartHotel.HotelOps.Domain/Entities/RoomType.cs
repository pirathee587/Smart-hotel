using SmartHotel.HotelOps.Domain.Common;

namespace SmartHotel.HotelOps.Domain.Entities;

public class RoomType : BaseEntity
{
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BedType { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int RoomSizeSqFt { get; set; }
    public decimal PricePerNight { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal AmenitiesFee { get; set; }
    public string LongDescription { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = new();
    public List<string> Amenities { get; set; } = new();
    public string CancellationPolicyText { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; } = true;

    public Hotel? Hotel { get; set; }
    public ICollection<RoomTypeImage> Images { get; set; } = new List<RoomTypeImage>();
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
