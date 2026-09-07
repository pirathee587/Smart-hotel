using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Features.Images.Commands;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.HotelOps.Tests;

public class TestImageStorageService : IImageStorageService
{
    public Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        return Task.FromResult($"https://storage.smarthotel.lk/images/{fileName}");
    }

    public Task DeleteImageAsync(string imageUrl, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}

public class ImageUploadValidationTests
{
    private HotelOpsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HotelOpsDbContext(options);
    }

    [Fact]
    public async Task ImageExceeding5MB_ShouldFailValidation()
    {
        using var context = CreateDbContext();
        var roomTypeId = Guid.NewGuid();
        context.RoomTypes.Add(new RoomType
        {
            Id = roomTypeId,
            HotelId = Guid.NewGuid(),
            Name = "Suite",
            PricePerNight = 100m,
            IsPublished = true
        });
        await context.SaveChangesAsync();

        var storage = new TestImageStorageService();
        var handler = new UploadRoomTypeImageCommandHandler(context, storage);

        long size6MB = 6 * 1024 * 1024;
        using var stream = new MemoryStream(new byte[100]);

        var command = new UploadRoomTypeImageCommand(
            roomTypeId,
            stream,
            "large_photo.jpg",
            "image/jpeg",
            size6MB);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("exceeds the maximum allowed size of 5MB");
    }

    [Theory]
    [InlineData("text/plain", "notes.txt")]
    [InlineData("application/pdf", "brochure.pdf")]
    [InlineData("application/x-msdownload", "virus.exe")]
    public async Task InvalidMimeType_ShouldFailValidation(string contentType, string fileName)
    {
        using var context = CreateDbContext();
        var roomTypeId = Guid.NewGuid();
        context.RoomTypes.Add(new RoomType
        {
            Id = roomTypeId,
            HotelId = Guid.NewGuid(),
            Name = "Suite",
            PricePerNight = 100m,
            IsPublished = true
        });
        await context.SaveChangesAsync();

        var storage = new TestImageStorageService();
        var handler = new UploadRoomTypeImageCommandHandler(context, storage);

        using var stream = new MemoryStream(new byte[100]);

        var command = new UploadRoomTypeImageCommand(
            roomTypeId,
            stream,
            fileName,
            contentType,
            FileSizeBytes: 1024);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Invalid image format");
    }

    [Theory]
    [InlineData("image/jpeg", "suite.jpg")]
    [InlineData("image/png", "suite.png")]
    [InlineData("image/webp", "suite.webp")]
    public async Task ValidImage_ShouldUploadAndPersist(string contentType, string fileName)
    {
        using var context = CreateDbContext();
        var roomTypeId = Guid.NewGuid();
        context.RoomTypes.Add(new RoomType
        {
            Id = roomTypeId,
            HotelId = Guid.NewGuid(),
            Name = "Suite",
            PricePerNight = 100m,
            IsPublished = true
        });
        await context.SaveChangesAsync();

        var storage = new TestImageStorageService();
        var handler = new UploadRoomTypeImageCommandHandler(context, storage);

        using var stream = new MemoryStream(new byte[1024]);

        var command = new UploadRoomTypeImageCommand(
            roomTypeId,
            stream,
            fileName,
            contentType,
            FileSizeBytes: 1024,
            IsPrimary: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ImageUrl.Should().Contain(fileName);
        result.Data.IsPrimary.Should().BeTrue();

        var persistedImage = await context.RoomTypeImages.FirstOrDefaultAsync(i => i.RoomTypeId == roomTypeId);
        persistedImage.Should().NotBeNull();
        persistedImage!.ImageUrl.Should().Contain(fileName);
    }
}
