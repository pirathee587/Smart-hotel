using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;

namespace SmartHotel.HotelOps.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(HotelOpsDbContext context, ILogger logger)
    {
        if (await context.Hotels.AnyAsync())
        {
            logger.LogInformation("Database already has hotel data. Skipping seeding.");
            return;
        }

        logger.LogInformation("Seeding initial Hotel Operations data...");

        // 1. Hotel
        var hotel = new Hotel
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "SmartHotel Colombo",
            Address = "123 Galle Road, Colombo 03, Sri Lanka",
            Phone = "+94 11 234 5678",
            Email = "info@smarthotel.lk",
            TotalFloors = 5,
            TotalRooms = 20
        };
        context.Hotels.Add(hotel);

        // 2. Departments
        var frontDesk = new Department
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222221"),
            HotelId = hotel.Id,
            Name = "Front Desk",
            Description = "Guest check-in, concierge, and reservations reception"
        };
        var housekeeping = new Department
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            HotelId = hotel.Id,
            Name = "Housekeeping",
            Description = "Room cleaning, linen inspection, and sanitization"
        };
        var maintenance = new Department
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222223"),
            HotelId = hotel.Id,
            Name = "Maintenance",
            Description = "Facility repairs, HVAC, electrical, and plumbing"
        };
        context.Departments.AddRange(frontDesk, housekeeping, maintenance);

        // 3. RoomTypes
        var deluxeOcean = new RoomType
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333331"),
            HotelId = hotel.Id,
            Name = "Deluxe Ocean Suite",
            Title = "Stunning Panoramic Ocean Views with Private Balcony",
            BedType = "King Bed",
            Capacity = 2,
            RoomSizeSqFt = 480,
            PricePerNight = 180.00m,
            CleaningFee = 25.00m,
            AmenitiesFee = 15.00m,
            LongDescription = "Experience coastal luxury with unhindered views of the Indian Ocean, premium marble bathroom, and rain shower.",
            Highlights = new List<string> { "Ocean Front View", "Private Balcony", "King Size Bed", "Free High-Speed Wi-Fi" },
            Amenities = new List<string> { "Air Conditioning", "Espresso Machine", "Mini Bar", "Smart TV", "Safe Deposit Box", "Bathrobes & Slippers" },
            CancellationPolicyText = "Free cancellation up to 48 hours before check-in. Non-refundable thereafter.",
            IsPublished = true,
            IsActive = true
        };

        var executiveCity = new RoomType
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333332"),
            HotelId = hotel.Id,
            Name = "Executive City Room",
            Title = "Modern Urban Comfort with Ergonomic Workspace",
            BedType = "Queen Bed",
            Capacity = 2,
            RoomSizeSqFt = 350,
            PricePerNight = 110.00m,
            CleaningFee = 20.00m,
            AmenitiesFee = 10.00m,
            LongDescription = "Tailored for business and leisure travelers alike, equipped with ergonomic workstation and soundproof glazing.",
            Highlights = new List<string> { "City Skyline View", "Ergonomic Desk", "Queen Bed", "High-Speed Wi-Fi" },
            Amenities = new List<string> { "Air Conditioning", "Coffee/Tea Maker", "Smart TV", "Safe Box", "Ironing Facilities" },
            CancellationPolicyText = "Free cancellation up to 24 hours before check-in.",
            IsPublished = true,
            IsActive = true
        };

        var presidentialPenthouse = new RoomType
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            HotelId = hotel.Id,
            Name = "Presidential Penthouse",
            Title = "Ultra-Luxury Penthouse with Rooftop Jacuzzi (Draft)",
            BedType = "2 King Beds",
            Capacity = 4,
            RoomSizeSqFt = 1200,
            PricePerNight = 650.00m,
            CleaningFee = 50.00m,
            AmenitiesFee = 35.00m,
            LongDescription = "The crown jewel of SmartHotel Colombo, featuring private rooftop terrace, dedicated butler pantry, and jacuzzi.",
            Highlights = new List<string> { "Rooftop Jacuzzi", "Dedicated Butler", "Private Terrace", "360 Panoramic Views" },
            Amenities = new List<string> { "Jacuzzi", "Full Kitchenette", "Premium Sound System", "Dining Room", "Walk-in Closet" },
            CancellationPolicyText = "Strict: 50% refund up to 7 days prior to arrival.",
            IsPublished = false, // DRAFT - Used to verify visibility filtering!
            IsActive = true
        };

        context.RoomTypes.AddRange(deluxeOcean, executiveCity, presidentialPenthouse);

        // Images for RoomTypes
        context.RoomTypeImages.AddRange(
            new RoomTypeImage
            {
                Id = Guid.NewGuid(),
                RoomTypeId = deluxeOcean.Id,
                ImageUrl = "https://images.unsplash.com/photo-1582719478250-c89cae4dc85b?w=1200",
                DisplayOrder = 1,
                IsPrimary = true
            },
            new RoomTypeImage
            {
                Id = Guid.NewGuid(),
                RoomTypeId = executiveCity.Id,
                ImageUrl = "https://images.unsplash.com/photo-1631049307264-da0ec9d70304?w=1200",
                DisplayOrder = 1,
                IsPrimary = true
            });

        // 4. Rooms across Floors
        var rooms = new List<Room>
        {
            // Floor 1
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = executiveCity.Id, RoomNumber = "101", Floor = 1, Status = RoomStatus.Available },
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = executiveCity.Id, RoomNumber = "102", Floor = 1, Status = RoomStatus.Occupied },
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = deluxeOcean.Id, RoomNumber = "103", Floor = 1, Status = RoomStatus.Dirty },
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = deluxeOcean.Id, RoomNumber = "104", Floor = 1, Status = RoomStatus.InCleaning },

            // Floor 2
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = executiveCity.Id, RoomNumber = "201", Floor = 2, Status = RoomStatus.Inspected },
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = executiveCity.Id, RoomNumber = "202", Floor = 2, Status = RoomStatus.Available },
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = deluxeOcean.Id, RoomNumber = "203", Floor = 2, Status = RoomStatus.Available },
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = deluxeOcean.Id, RoomNumber = "204", Floor = 2, Status = RoomStatus.OutOfOrder, OutOfOrderReason = "AC unit replacement pending maintenance" },

            // Floor 3
            new() { Id = Guid.NewGuid(), HotelId = hotel.Id, RoomTypeId = presidentialPenthouse.Id, RoomNumber = "301", Floor = 3, Status = RoomStatus.Available }
        };

        context.Rooms.AddRange(rooms);

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded Hotel, 3 Departments, 3 RoomTypes, and {RoomCount} Rooms.", rooms.Count);
    }
}
