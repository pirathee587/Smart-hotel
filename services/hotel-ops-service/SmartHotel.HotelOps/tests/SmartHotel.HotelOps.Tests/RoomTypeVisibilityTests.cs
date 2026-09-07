using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Features.RoomTypes.Commands;
using SmartHotel.HotelOps.Application.Features.RoomTypes.Queries;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.HotelOps.Tests;

public class RoomTypeVisibilityTests
{
    private HotelOpsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HotelOpsDbContext(options);
    }

    [Fact]
    public async Task CustomerOrAnonymous_ShouldOnlySeePublishedRoomTypes()
    {
        using var context = CreateDbContext();
        var hotelId = Guid.NewGuid();

        var published1 = new RoomType
        {
            Id = Guid.NewGuid(),
            HotelId = hotelId,
            Name = "Deluxe Ocean Suite",
            Title = "Ocean View",
            PricePerNight = 150m,
            IsPublished = true,
            IsActive = true
        };
        var published2 = new RoomType
        {
            Id = Guid.NewGuid(),
            HotelId = hotelId,
            Name = "Standard Room",
            Title = "Cozy Comfort",
            PricePerNight = 80m,
            IsPublished = true,
            IsActive = true
        };
        var draft = new RoomType
        {
            Id = Guid.NewGuid(),
            HotelId = hotelId,
            Name = "Presidential Penthouse",
            Title = "Draft Luxury",
            PricePerNight = 500m,
            IsPublished = false, // DRAFT
            IsActive = true
        };

        context.RoomTypes.AddRange(published1, published2, draft);
        await context.SaveChangesAsync();

        var handler = new GetRoomTypesQueryHandler(context);

        // Act: Non-staff query
        var customerResult = await handler.Handle(new GetRoomTypesQuery(IsStaff: false), CancellationToken.None);

        // Assert: Customer only sees the 2 published types
        customerResult.Succeeded.Should().BeTrue();
        customerResult.Data.Should().HaveCount(2);
        customerResult.Data!.Select(r => r.Name).Should().Contain(new[] { "Deluxe Ocean Suite", "Standard Room" });
        customerResult.Data!.Select(r => r.Name).Should().NotContain("Presidential Penthouse");

        // Act: Staff query
        var staffResult = await handler.Handle(new GetRoomTypesQuery(IsStaff: true), CancellationToken.None);

        // Assert: Staff sees all 3 types including draft
        staffResult.Succeeded.Should().BeTrue();
        staffResult.Data.Should().HaveCount(3);
        staffResult.Data!.Select(r => r.Name).Should().Contain("Presidential Penthouse");
    }

    [Fact]
    public async Task GetById_DraftRoomType_ReturnsNotFoundForCustomer_AndSucceedsForStaff()
    {
        using var context = CreateDbContext();
        var draftId = Guid.NewGuid();

        var draft = new RoomType
        {
            Id = draftId,
            HotelId = Guid.NewGuid(),
            Name = "Draft Suite",
            PricePerNight = 200m,
            IsPublished = false,
            IsActive = true
        };

        context.RoomTypes.Add(draft);
        await context.SaveChangesAsync();

        var getHandler = new GetRoomTypeByIdQueryHandler(context);

        // Customer cannot access unpublished
        var customerResult = await getHandler.Handle(new GetRoomTypeByIdQuery(draftId, IsStaff: false), CancellationToken.None);
        customerResult.Succeeded.Should().BeFalse();

        // Staff can access unpublished
        var staffResult = await getHandler.Handle(new GetRoomTypeByIdQuery(draftId, IsStaff: true), CancellationToken.None);
        staffResult.Succeeded.Should().BeTrue();
        staffResult.Data!.Name.Should().Be("Draft Suite");

        // Act: Admin publishes the draft
        var publishHandler = new PublishRoomTypeCommandHandler(context);
        var publishResult = await publishHandler.Handle(new PublishRoomTypeCommand(draftId), CancellationToken.None);
        publishResult.Succeeded.Should().BeTrue();

        // Assert: Customer can now access published room type
        var afterPublishResult = await getHandler.Handle(new GetRoomTypeByIdQuery(draftId, IsStaff: false), CancellationToken.None);
        afterPublishResult.Succeeded.Should().BeTrue();
        afterPublishResult.Data!.IsPublished.Should().BeTrue();
    }
}
