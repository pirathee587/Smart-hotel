using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace SmartHotel.Gateway.IntegrationTests;

public class RouteAuthorizationTests : IClassFixture<CustomGatewayFactory>
{
    private readonly CustomGatewayFactory _factory;
    private readonly HttpClient _client;

    public RouteAuthorizationTests(CustomGatewayFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PublicRoute_CustomerRegister_WithoutJwt_SucceedsWithout401()
    {
        // Act: call public registration route without Authorization header
        var content = new StringContent("{\"email\":\"test@smarthotel.com\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/customers/register", content);

        // Assert: Gateway passes public route through (does NOT return 401)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedRoute_AuthMe_WithoutJwt_Returns401Unauthorized()
    {
        // Act: call protected route without Authorization header
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert: Gateway rejects with 401 Unauthorized before reaching downstream
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedRoute_AuthMe_WithInvalidJwt_Returns401Unauthorized()
    {
        // Arrange: prepare invalid / forged JWT
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedRoute_AuthMe_WithValidCustomerJwt_PassesGateway()
    {
        // Arrange: generate a valid RS256 token signed by the key matching Gateway's JWKS
        var token = _factory.JwtHelper.GenerateToken(sub: Guid.NewGuid().ToString(), role: "Customer");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Gateway successfully authenticates and proxies request
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("User profile retrieved successfully");
    }

    [Fact]
    public async Task PublicRoute_Jwks_WithoutJwt_SucceedsWithout401()
    {
        // Act
        var response = await _client.GetAsync("/.well-known/jwks.json");

        // Assert: Gateway serves/proxies JWKS without requiring a JWT
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NotificationRoute_WithoutJwt_Returns401Unauthorized()
    {
        // Act: call protected notifications endpoint without JWT
        var response = await _client.GetAsync("/api/v1/notifications/my");

        // Assert: rejected by Gateway
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChatRoute_WithoutJwt_Returns401Unauthorized()
    {
        // Act: call protected chat endpoint without JWT
        var response = await _client.GetAsync("/api/v1/chat/rooms/my");

        // Assert: rejected by Gateway
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NotificationRoute_WithValidJwt_ProxiesSuccessfully()
    {
        // Arrange
        var token = _factory.JwtHelper.GenerateToken(sub: Guid.NewGuid().ToString(), role: "Employee");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/my");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Gateway accepts and proxies to downstream
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignalRHub_NegotiateEndpoint_AllowsAnonymousGatewayPassThrough()
    {
        // Act: SignalR clients initiate connection via /hubs/notifications/negotiate
        var response = await _client.PostAsync("/hubs/notifications/negotiate", null);

        // Assert: Gateway allows anonymous handshake pass-through to downstream service
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConciergeRoute_WithoutJwt_Returns401Unauthorized()
    {
        // Act: call concierge chat stream without JWT
        var content = new StringContent("{\"message\":\"Hello\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/concierge/chat/stream", content);

        // Assert: rejected by Gateway before reaching downstream
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConciergeRoute_WithValidCustomerJwt_ProxiesSuccessfully()
    {
        // Arrange: generate valid Customer JWT
        var token = _factory.JwtHelper.GenerateToken(sub: Guid.NewGuid().ToString(), role: "Customer");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/concierge/chat/stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{\"message\":\"Hello\"}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Gateway validates JWT and proxies to downstream
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/v1/finance/operations/transactions")]
    [InlineData("/api/v1/finance/audit")]
    [InlineData("/api/v1/escrow/releases")]
    public async Task FinancialRoutes_WithoutJwt_AreRejectedAtGateway(string path)
    {
        var response = await _client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
