using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Features.Images.Queries;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.HotelOps.Tests;

public class RoomTypeImagesQueryTests
{
    private HotelOpsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HotelOpsDbContext(options);
    }

    [Fact]
    public async Task GetRoomTypeImages_WhenRoomTypeDoesNotExist_ShouldReturnFailure()
    {
        using var context = CreateDbContext();
        var handler = new GetRoomTypeImagesQueryHandler(context);
        var query = new GetRoomTypeImagesQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("was not found");
    }

    [Fact]
    public async Task GetRoomTypeImages_WhenImagesExist_ShouldReturnOrderedImages()
    {
        using var context = CreateDbContext();
        var roomTypeId = Guid.NewGuid();
        var roomType = new RoomType
        {
            Id = roomTypeId,
            HotelId = Guid.NewGuid(),
            Name = "Presidential Suite",
            PricePerNight = 500m,
            IsPublished = true,
            Images = new List<RoomTypeImage>
            {
                new() { Id = Guid.NewGuid(), RoomTypeId = roomTypeId, ImageUrl = "https://supabase/rooms/img2.jpg", DisplayOrder = 2, IsPrimary = false },
                new() { Id = Guid.NewGuid(), RoomTypeId = roomTypeId, ImageUrl = "https://supabase/rooms/img1.jpg", DisplayOrder = 1, IsPrimary = true },
                new() { Id = Guid.NewGuid(), RoomTypeId = roomTypeId, ImageUrl = "https://supabase/rooms/img3.jpg", DisplayOrder = 3, IsPrimary = false }
            }
        };

        context.RoomTypes.Add(roomType);
        await context.SaveChangesAsync();

        var handler = new GetRoomTypeImagesQueryHandler(context);
        var query = new GetRoomTypeImagesQuery(roomTypeId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(3);
        result.Data![0].DisplayOrder.Should().Be(1);
        result.Data[0].IsPrimary.Should().BeTrue();
        result.Data[1].DisplayOrder.Should().Be(2);
        result.Data[2].DisplayOrder.Should().Be(3);
    }
}
