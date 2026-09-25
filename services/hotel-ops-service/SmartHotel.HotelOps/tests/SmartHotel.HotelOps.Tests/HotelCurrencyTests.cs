using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.HotelOps.API.Services;
using SmartHotel.HotelOps.Application.Features.RoomTypes.Queries;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Grpc;
using SmartHotel.HotelOps.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.HotelOps.Tests;

public class HotelCurrencyTests
{
    private static HotelOpsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new HotelOpsDbContext(options);
    }

    [Fact]
    public void Hotel_DefaultCurrency_IsAuthoritativeLKR()
    {
        var hotel = new Hotel();
        hotel.BaseCurrency.Should().Be("LKR");
    }

    [Fact]
    public async Task GetRoomTypeById_ReturnsAuthoritativeHotelCurrency()
    {
        using var context = CreateDbContext();
        var hotel = new Hotel
        {
            Id = Guid.NewGuid(),
            Name = "SmartHotel Maskeliya",
            BaseCurrency = "LKR"
        };
        var roomType = new RoomType
        {
            Id = Guid.NewGuid(),
            HotelId = hotel.Id,
            Name = "Deluxe Ocean Suite",
            PricePerNight = 180m,
            IsPublished = true,
            IsActive = true
        };
        context.Hotels.Add(hotel);
        context.RoomTypes.Add(roomType);
        await context.SaveChangesAsync();

        var handler = new GetRoomTypeByIdQueryHandler(context);
        var result = await handler.Handle(new GetRoomTypeByIdQuery(roomType.Id, IsStaff: false), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Currency.Should().Be("LKR");
    }

    [Fact]
    public async Task GetRoomTypes_InheritsAuthoritativeHotelCurrency()
    {
        using var context = CreateDbContext();
        var hotel = new Hotel
        {
            Id = Guid.NewGuid(),
            Name = "SmartHotel Maskeliya",
            BaseCurrency = "LKR"
        };
        var roomType = new RoomType
        {
            Id = Guid.NewGuid(),
            HotelId = hotel.Id,
            Name = "Grand Penthouse",
            PricePerNight = 350m,
            IsPublished = true,
            IsActive = true
        };
        context.Hotels.Add(hotel);
        context.RoomTypes.Add(roomType);
        await context.SaveChangesAsync();

        var handler = new GetRoomTypesQueryHandler(context);
        var result = await handler.Handle(new GetRoomTypesQuery(IsStaff: false), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().ContainSingle(r => r.Currency == "LKR");
    }
}
