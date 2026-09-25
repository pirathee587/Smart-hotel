using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.HotelOps.Infrastructure.Storage;
using Xunit;

namespace SmartHotel.HotelOps.Tests;

public class SupabaseStorageServiceTests
{
    private readonly HttpClient _httpClient = new();

    [Fact]
    public void Constructor_WhenSupabaseBucketConfigured_InitializesSuccessfully()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Supabase:Bucket", "hotel-images" },
            { "Supabase:Url", "https://example.supabase.co" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var service = new SupabaseStorageService(_httpClient, configuration, NullLogger<SupabaseStorageService>.Instance);

        // Assert
        service.Should().NotBeNull();
        service.GetPublicUrl("rooms/photo.jpg").Should().Be("https://example.supabase.co/storage/v1/object/public/hotel-images/rooms/photo.jpg");
    }

    [Fact]
    public void Constructor_WhenOnlyLegacyBucketNameConfigured_FallsBackSuccessfully()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Supabase:BucketName", "hotel-images" },
            { "Supabase:Url", "https://example.supabase.co" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var service = new SupabaseStorageService(_httpClient, configuration, NullLogger<SupabaseStorageService>.Instance);

        // Assert
        service.Should().NotBeNull();
        service.GetPublicUrl("rooms/photo.jpg").Should().Be("https://example.supabase.co/storage/v1/object/public/hotel-images/rooms/photo.jpg");
    }

    [Fact]
    public void Constructor_WhenBucketNameMissingInConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Supabase:Url", "https://example.supabase.co" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        Action act = () => new SupabaseStorageService(_httpClient, configuration, NullLogger<SupabaseStorageService>.Instance);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Supabase storage bucket name is not configured in appsettings.json (Supabase:Bucket).*");
    }
}
