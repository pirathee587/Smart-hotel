using System.Net;
using FluentAssertions;
using Xunit;

namespace SmartHotel.Gateway.IntegrationTests;

public class HealthCheckTests : IClassFixture<CustomGatewayFactory>
{
    private readonly CustomGatewayFactory _factory;
    private readonly HttpClient _client;

    public HealthCheckTests(CustomGatewayFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_WithoutJwt_Returns200AndAggregateHealthStatus()
    {
        // Act: call gateway /health endpoint (public)
        var response = await _client.GetAsync("/health");

        // Assert: 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"status\":\"Healthy\"");
        content.Should().Contain("\"gateway\":\"Healthy\"");
        content.Should().Contain("\"identity-service\"");
        content.Should().Contain("\"notifications-service\"");
        content.Should().Contain("\"concierge-service\"");
    }
}
