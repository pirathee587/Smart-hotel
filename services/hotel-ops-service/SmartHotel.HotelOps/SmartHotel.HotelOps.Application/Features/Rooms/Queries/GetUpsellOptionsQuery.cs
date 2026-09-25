using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Application.Features.Rooms.Queries;

public record UpsellOptionDto
{
    public Guid RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public Guid RoomTypeId { get; init; }
    public string RatePlanId { get; init; } = string.Empty;
    public string RatePlanName { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public List<string> GalleryImages { get; init; } = new();
    public int BedCount { get; init; } = 1;
    public string BedDescription { get; init; } = string.Empty;
    public int Sleeps { get; init; } = 2;
    public int RoomSizeSqFt { get; init; }
    public decimal CurrentPrice { get; init; }
    public decimal UpgradePrice { get; init; }
    public decimal PriceDelta { get; init; }
    public string ShortDescription { get; init; } = string.Empty;
    public string FullDescription { get; init; } = string.Empty;
    public List<string> Amenities { get; init; } = new();
    public string Badge { get; init; } = string.Empty;
}

public record GetUpsellOptionsQuery(
    string RoomId,
    DateOnly? CheckIn = null,
    DateOnly? CheckOut = null,
    int? Guests = null,
    decimal? CurrentPrice = null
) : IRequest<Result<List<UpsellOptionDto>>>;

public class GetUpsellOptionsQueryHandler : IRequestHandler<GetUpsellOptionsQuery, Result<List<UpsellOptionDto>>>
{
    private readonly IHotelOpsDbContext _dbContext;

    public GetUpsellOptionsQueryHandler(IHotelOpsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<UpsellOptionDto>>> Handle(GetUpsellOptionsQuery request, CancellationToken ct)
    {
        // 1. Fetch published room types
        var allRoomTypes = await _dbContext.RoomTypes
            .Include(rt => rt.Images)
            .Where(rt => rt.IsPublished && rt.IsActive)
            .OrderBy(rt => rt.PricePerNight)
            .ToListAsync(ct);

        // Find current room type if requested by Guid or fallback to first tier
        decimal currentPrice = request.CurrentPrice ?? 315m;
        if (Guid.TryParse(request.RoomId, out var currentGuid))
        {
            var matched = allRoomTypes.FirstOrDefault(rt => rt.Id == currentGuid);
            if (matched != null)
            {
                currentPrice = matched.PricePerNight;
            }
        }

        // Filter higher tier room types (price > currentPrice, or other tiers)
        var upgradeRoomTypes = allRoomTypes.Where(rt => rt.PricePerNight > currentPrice).ToList();
        if (!upgradeRoomTypes.Any())
        {
            upgradeRoomTypes = allRoomTypes.Take(2).ToList();
        }

        var options = new List<UpsellOptionDto>();

        foreach (var rt in upgradeRoomTypes)
        {
            var primaryImg = rt.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                ?? rt.Images.FirstOrDefault()?.ImageUrl 
                ?? "/images/slowhouse-interior.jpg";

            var delta = Math.Max(35m, rt.PricePerNight - currentPrice);

            options.Add(new UpsellOptionDto
            {
                RoomId = rt.Id,
                RoomName = rt.Name,
                RoomTypeId = rt.Id,
                RatePlanId = "rp-member-exclusive",
                RatePlanName = "Member Exclusive — All Inclusive & Geothermal Ritual",
                ImageUrl = primaryImg,
                GalleryImages = rt.Images.Select(i => i.ImageUrl).ToList(),
                BedCount = rt.Capacity > 2 ? 2 : 1,
                BedDescription = rt.BedType,
                Sleeps = rt.Capacity,
                RoomSizeSqFt = rt.RoomSizeSqFt,
                CurrentPrice = currentPrice,
                UpgradePrice = rt.PricePerNight,
                PriceDelta = delta,
                ShortDescription = string.IsNullOrWhiteSpace(rt.Title) 
                    ? "Upgraded sanctuary residence featuring expanded panoramic terrace and artisan baths."
                    : rt.Title,
                FullDescription = rt.LongDescription,
                Amenities = rt.Amenities.Count > 0 
                    ? rt.Amenities 
                    : new List<string>
                    {
                        "Air conditioning", "In-room WiFi", "Geothermal soak tub",
                        "Rainfall shower", "Coffee/tea maker", "Botanical amenities",
                        "Minibar", "Safe", "Balcony terrace"
                    },
                Badge = "Recommended Upgrade"
            });
        }

        // If database had empty room types, provide fallback high-tier options matching SmartHotel property
        if (options.Count == 0)
        {
            options.Add(new UpsellOptionDto
            {
                RoomId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                RoomName = "Signature Slowhouse Suite",
                RoomTypeId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                RatePlanId = "rp-slowhouse-member",
                RatePlanName = "Member Exclusive — All Inclusive & Geothermal Ritual",
                ImageUrl = "/images/slowhouse-interior.jpg",
                GalleryImages = new List<string> { "/images/slowhouse-interior.jpg", "/images/slowhouse-hero.jpg" },
                BedCount = 1,
                BedDescription = "1 Master King Bed (200 × 200 cm)",
                Sleeps = 3,
                RoomSizeSqFt = 780,
                CurrentPrice = currentPrice,
                UpgradePrice = 430m,
                PriceDelta = 115m,
                ShortDescription = "Architectural slowhouse featuring exposed rammed-earth walls, private geothermal cedar tub, and roaring evening fireplace.",
                FullDescription = "A sanctuary of acoustic tranquility and tactile comfort. The Signature Slowhouse Suite features double-height glazed openings framing Sri Pada peak, a sunken fireside lounge, and a private spring-fed cedar soaking tub fed directly by mineral aquifers.",
                Amenities = new List<string>
                {
                    "Air conditioning", "Smoke detectors", "In-room WiFi", "Geothermal soak tub",
                    "Ironing board", "Coffee/tea maker", "Rainfall shower", "Emergency exit maps",
                    "Desk with lamp", "Cable TV", "Bottled water", "Hairdryer", "Safe", "Minibar",
                    "Bathroom amenities", "Evening fireplace lounge"
                },
                Badge = "Most Popular Upgrade"
            });

            options.Add(new UpsellOptionDto
            {
                RoomId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                RoomName = "Ridge Panorama Suite",
                RoomTypeId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                RatePlanId = "rp-ridge-member",
                RatePlanName = "Member Rate — Ridge Sanctuary Package",
                ImageUrl = "/images/ridge-thumb.jpg",
                GalleryImages = new List<string> { "/images/ridge-thumb.jpg", "/images/hero-view.jpg" },
                BedCount = 2,
                BedDescription = "1 King Bed + 1 Queen Bed",
                Sleeps = 4,
                RoomSizeSqFt = 920,
                CurrentPrice = currentPrice,
                UpgradePrice = 495m,
                PriceDelta = 180m,
                ShortDescription = "Elevated high atop the southern ridge line with unobstructed 270° panoramic views over the Maskeliya reservoir.",
                FullDescription = "Perched along the high windswept ridge, these dual-bedroom suites offer uninterrupted sunset views across Maussakelle reservoir. Featuring natural slate finishes, twin rainfall showers, and an expansive cantilevered timber observation deck.",
                Amenities = new List<string>
                {
                    "Air conditioning", "In-room WiFi", "Panoramic ridge observation deck",
                    "Dual en-suite bathrooms", "Stargazing telescope", "Coffee/tea maker",
                    "Bottled water", "Hairdryer", "Safe", "Minibar", "Bathroom amenities"
                },
                Badge = "Spectacular Views"
            });
        }

        return Result<List<UpsellOptionDto>>.Success(options);
    }
}
