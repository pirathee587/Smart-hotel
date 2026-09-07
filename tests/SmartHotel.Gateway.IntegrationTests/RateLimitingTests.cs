using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SmartHotel.Gateway.IntegrationTests;

public class RateLimitingTests : IClassFixture<CustomGatewayFactory>
{
    private readonly CustomGatewayFactory _factory;
    private readonly HttpClient _client;

    public RateLimitingTests(CustomGatewayFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ExceedingRateLimit_Returns429WithRetryAfterHeader()
    {
        // Arrange: In test config, Customer limit is 3 tokens
        var userId = Guid.NewGuid().ToString();
        var token = _factory.JwtHelper.GenerateToken(sub: userId, role: "Customer");

        // Act: send 4 consecutive requests (threshold = 3)
        HttpResponseMessage? lastResponse = null;
        for (int i = 0; i < 4; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            lastResponse = await _client.SendAsync(request);
        }

        // Assert: 4th request must be rejected with 429 Too Many Requests
        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be((HttpStatusCode)429);

        // Assert: Retry-After header must be present
        lastResponse.Headers.Contains("Retry-After").Should().BeTrue();
        var retryAfterValue = lastResponse.Headers.GetValues("Retry-After").FirstOrDefault();
        retryAfterValue.Should().NotBeNullOrEmpty();
        int.TryParse(retryAfterValue, out var seconds).Should().BeTrue();
        seconds.Should().BeGreaterThan(0);

        // Assert: Response body contains error payload
        var body = await lastResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Too Many Requests");
        body.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public async Task RateLimiting_UserPartitionIsolation_UserAHittingLimitDoesNotBlockUserB()
    {
        // Arrange: User A and User B with distinct 'sub' claims
        var userAId = "user-a-" + Guid.NewGuid();
        var userBId = "user-b-" + Guid.NewGuid();

        var tokenA = _factory.JwtHelper.GenerateToken(sub: userAId, role: "Customer");
        var tokenB = _factory.JwtHelper.GenerateToken(sub: userBId, role: "Customer");

        // Act 1: Exhaust User A's rate limit bucket (Customer capacity = 3)
        for (int i = 0; i < 3; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Verify User A is now rate-limited
        var reqAOver = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        reqAOver.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var resAOver = await _client.SendAsync(reqAOver);
        resAOver.StatusCode.Should().Be((HttpStatusCode)429);

        // Act 2: User B sends a request immediately after User A was rate-limited
        var reqB = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        reqB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var resB = await _client.SendAsync(reqB);

        // Assert: User B is NOT affected by User A's limit!
        resB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RateLimiting_RoleTiers_AdminAllowsMoreRequestsThanCustomer()
    {
        // Arrange: Customer limit = 3, Admin limit = 10
        var adminId = "admin-" + Guid.NewGuid();
        var adminToken = _factory.JwtHelper.GenerateToken(sub: adminId, role: "Admin");

        // Act: Admin sends 5 consecutive requests (which would exceed Customer's 3-token limit)
        for (int i = 0; i < 5; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK, $"Admin request #{i + 1} should succeed within Admin's 10-token tier");
        }
    }

    [Fact]
    public async Task RateLimiting_AnonymousEndpoint_PartitionsByClientIp()
    {
        // Arrange: Anonymous tier limit = 2
        var ipA = "10.0.0." + new Random().Next(1, 100);
        var ipB = "10.0.0." + new Random().Next(101, 200);

        // Act 1: Send 2 requests from IP A to consume bucket
        for (int i = 0; i < 2; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/register");
            req.Headers.Add("X-Forwarded-For", ipA);
            req.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 3rd request from IP A hits rate limit
        var reqAOver = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/register");
        reqAOver.Headers.Add("X-Forwarded-For", ipA);
        reqAOver.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var resAOver = await _client.SendAsync(reqAOver);
        resAOver.StatusCode.Should().Be((HttpStatusCode)429);

        // Act 2: Request from IP B must succeed independently
        var reqB = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/register");
        reqB.Headers.Add("X-Forwarded-For", ipB);
        reqB.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var resB = await _client.SendAsync(reqB);
        resB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RateLimiting_TwoTierDefenseInDepth_GatewayPassesUpTo30ButIdentityBlocksAt5PerHour()
    {
        // Arrange: Custom client where Gateway Anonymous tier is set to 30 req/min (production setting)
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Anonymous:TokenLimit"] = "30",
                    ["RateLimiting:Anonymous:TokensPerPeriod"] = "30",
                    ["RateLimiting:Anonymous:ReplenishmentPeriodSeconds"] = "60",
                    ["RateLimiting:Anonymous:QueueLimit"] = "0"
                });
            });
        }).CreateClient();

        var targetEmail = $"forgotpass-{Guid.NewGuid()}@smarthotel.com";
        var uniqueIp = "10.10.10." + new Random().Next(1, 250);

        // Act & Assert 1: First 5 requests must pass BOTH Gateway (30/min limit) AND Identity Service (5/hour limit)
        for (int i = 1; i <= 5; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/forgot-password");
            request.Headers.Add("X-Forwarded-For", uniqueIp);
            request.Content = new StringContent(
                $"{{\"email\":\"{targetEmail}\"}}",
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"Attempt #{i} is within Identity Service's 5/hour limit and Gateway's 30/min limit");

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("password reset link has been sent");
        }

        // Act 2: 6th request within the same minute:
        // - Easily passes the Gateway's Anonymous limit (6 <= 30 tokens available)
        // - But gets blocked by Identity Service's application-layer InMemoryRateLimiterService (max 5/hour)
        var sixthRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/forgot-password");
        sixthRequest.Headers.Add("X-Forwarded-For", uniqueIp);
        sixthRequest.Content = new StringContent(
            $"{{\"email\":\"{targetEmail}\"}}",
            System.Text.Encoding.UTF8,
            "application/json");

        var sixthResponse = await client.SendAsync(sixthRequest);

        // Assert: 6th response is HTTP 429 Too Many Requests
        sixthResponse.StatusCode.Should().Be((HttpStatusCode)429);

        // Assert: The rejection comes from Identity Service's InMemoryRateLimiterService
        var sixthBody = await sixthResponse.Content.ReadAsStringAsync();
        sixthBody.Should().Contain("Too many password reset requests. Please try again later.");

        // Assert: Gateway did NOT block it (Gateway error response would have contained "Rate limit exceeded. Please try again later.")
        // This explicitly proves neither layer masks or interferes with the other!
        sixthBody.Should().NotContain("Rate limit exceeded. Please try again later.");
    }
}
