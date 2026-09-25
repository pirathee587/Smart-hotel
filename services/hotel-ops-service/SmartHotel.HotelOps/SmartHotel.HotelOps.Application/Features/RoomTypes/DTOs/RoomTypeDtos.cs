namespace SmartHotel.HotelOps.Application.Features.RoomTypes.DTOs;

public class RoomTypeDto
{
    public Guid Id { get; set; }
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
    public bool IsActive { get; set; }
    public string Currency { get; set; } = "LKR";
    public List<RoomTypeImageDto> Images { get; set; } = new();
}

public class RoomTypeImageDto
{
    public Guid Id { get; set; }
    public Guid RoomTypeId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}

public class CreateRoomTypeRequest
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
}

public class UpdateRoomTypeRequest
{
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
    public bool IsActive { get; set; } = true;
}
