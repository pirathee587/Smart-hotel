using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Xunit;

namespace SmartHotel.Gateway.IntegrationTests;

public class CrossServiceGatewayTests : IClassFixture<CustomGatewayFactory>
{
    private readonly CustomGatewayFactory _factory;
    private readonly HttpClient _client;

    public CrossServiceGatewayTests(CustomGatewayFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_Aggregated_ReturnsBothIdentityAndHotelOpsServices()
    {
        // Act: Request gateway aggregate health check
        var response = await _client.GetAsync("/health");

        // Assert: 200 OK with both downstream services reporting Healthy
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        content.Should().Contain("\"status\":\"Healthy\"");
        content.Should().Contain("\"gateway\":\"Healthy\"");
        content.Should().Contain("\"identity-service\"");
        content.Should().Contain("\"hotel-ops-service\"");
    }

    [Fact]
    public async Task QueryRoomTypes_WithoutToken_AllowsPublicAccess()
    {
        // Act: Anonymous customer queries available room types
        var response = await _client.GetAsync("/api/v1/room-types");

        // Assert: 200 OK returned through gateway to HotelOps service
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Deluxe Ocean Suite");
    }

    [Fact]
    public async Task CreateRoomType_WithoutToken_Returns401UnauthorizedAtGateway()
    {
        // Arrange: Prepare create room type request with no Authorization header
        var payload = new StringContent("{\"name\":\"Executive Suite\"}", Encoding.UTF8, "application/json");

        // Act: POST to protected endpoint without credentials
        var response = await _client.PostAsync("/api/v1/room-types", payload);

        // Assert: Gateway rejects immediately with 401 Unauthorized before forwarding
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateRoomType_WithCustomerJwt_PassesGateway_Returns403ForbiddenFromHotelOps()
    {
        // Arrange: Customer JWT issued by Identity Service
        var customerToken = _factory.JwtHelper.GenerateToken(sub: "cust-12345", role: "Customer");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/room-types")
        {
            Content = new StringContent("{\"name\":\"Customer Penthouse Attempt\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        // Act: Customer attempts admin-only resource creation
        var response = await _client.SendAsync(request);

        // Assert: Gateway authenticates successfully, HotelOps validates claims and returns 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Admin role required");
    }

    [Fact]
    public async Task CreateRoomType_WithAdminJwt_PassesGatewayAndHotelOps_Returns201Created()
    {
        // Arrange: Admin JWT issued by Identity Service
        var adminToken = _factory.JwtHelper.GenerateToken(sub: "admin-99999", role: "Admin");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/room-types")
        {
            Content = new StringContent("{\"name\":\"New Penthouse\",\"pricePerNight\":500}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act: Admin creates a new room type
        var response = await _client.SendAsync(request);

        // Assert: 201 Created from downstream HotelOps service
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("New Penthouse");
    }

    [Fact]
    public async Task RateLimitIsolation_ExhaustingHotelOpsBucket_DoesNotDepleteIdentityServiceBucket()
    {
        // Arrange: Anonymous rate limit is configured to 2 tokens in test environment
        var isolatedClient = _factory.CreateClient();
        string clientIp = "198.51.100.77";

        // Helper to construct request with specific IP
        HttpRequestMessage CreateHotelOpsRequest()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/room-types");
            req.Headers.Add("X-Forwarded-For", clientIp);
            return req;
        }

        HttpRequestMessage CreateIdentityRequest()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/register");
            req.Headers.Add("X-Forwarded-For", clientIp);
            req.Content = new StringContent("{\"email\":\"guest@smarthotel.lk\"}", Encoding.UTF8, "application/json");
            return req;
        }

        // Act 1: Send 2 requests to HotelOps (exhausts the 2-token limit for hotelops-service)
        var resp1 = await isolatedClient.SendAsync(CreateHotelOpsRequest());
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);

        var resp2 = await isolatedClient.SendAsync(CreateHotelOpsRequest());
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: 3rd request to HotelOps should be throttled (429 Too Many Requests)
        var resp3 = await isolatedClient.SendAsync(CreateHotelOpsRequest());
        resp3.StatusCode.Should().Be((HttpStatusCode)429);

        // Act 3: Immediately call Identity Service from the same client IP
        var identityResp = await isolatedClient.SendAsync(CreateIdentityRequest());

        // Assert: Identity Service request SUCCEEDS with 200 OK because rate-limits are partitioned per service!
        identityResp.StatusCode.Should().Be(HttpStatusCode.OK, "Identity Service rate-limit bucket must be isolated from Hotel Ops bucket");
    }

    [Fact]
    public async Task FullCrossServiceLifecycle_RegisterLoginQuery_AdminCreatePublish_CustomerForbidden()
    {
        // ---------------------------------------------------------------------
        // Step 1: Register customer on Identity Service through Gateway
        // ---------------------------------------------------------------------
        var registerPayload = new StringContent(
            "{\"email\":\"jane.doe@example.com\",\"password\":\"SecurePass123!\",\"firstName\":\"Jane\",\"lastName\":\"Doe\"}",
            Encoding.UTF8,
            "application/json");
        var registerResp = await _client.PostAsync("/api/v1/customers/register", registerPayload);
        registerResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // ---------------------------------------------------------------------
        // Step 2: Customer logs in through Gateway, obtains RS256 JWT
        // ---------------------------------------------------------------------
        var loginPayload = new StringContent(
            "{\"email\":\"jane.doe@example.com\",\"password\":\"SecurePass123!\"}",
            Encoding.UTF8,
            "application/json");
        var loginResp = await _client.PostAsync("/api/v1/customers/login", loginPayload);
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginJson = await loginResp.Content.ReadAsStringAsync();
        using var loginDoc = System.Text.Json.JsonDocument.Parse(loginJson);
        var customerToken = loginDoc.RootElement.GetProperty("accessToken").GetString();
        customerToken.Should().NotBeNullOrWhiteSpace();

        // ---------------------------------------------------------------------
        // Step 3: Customer queries published room types through Gateway
        // ---------------------------------------------------------------------
        var queryReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/room-types");
        queryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var queryResp = await _client.SendAsync(queryReq);
        queryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var queryContent = await queryResp.Content.ReadAsStringAsync();
        queryContent.Should().Contain("Deluxe Ocean Suite");

        // ---------------------------------------------------------------------
        // Step 4: Customer attempts to create room type (Admin only) -> 403 Forbidden
        // ---------------------------------------------------------------------
        var forbiddenReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/room-types")
        {
            Content = new StringContent("{\"name\":\"Customer Unauthorized Suite\"}", Encoding.UTF8, "application/json")
        };
        forbiddenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var forbiddenResp = await _client.SendAsync(forbiddenReq);
        forbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // ---------------------------------------------------------------------
        // Step 5: Admin logs in through Gateway, obtains Admin RS256 JWT
        // ---------------------------------------------------------------------
        var adminLoginReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/employee/login")
        {
            Content = new StringContent(
                "{\"email\":\"admin@smarthotel.lk\",\"password\":\"AdminPass123!\"}",
                Encoding.UTF8,
                "application/json")
        };
        adminLoginReq.Headers.Add("X-Forwarded-For", "10.0.0.99");
        var adminLoginResp = await _client.SendAsync(adminLoginReq);
        adminLoginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminJson = await adminLoginResp.Content.ReadAsStringAsync();
        using var adminDoc = System.Text.Json.JsonDocument.Parse(adminJson);
        var adminToken = adminDoc.RootElement.GetProperty("accessToken").GetString();
        adminToken.Should().NotBeNullOrWhiteSpace();

        // ---------------------------------------------------------------------
        // Step 6: Admin creates new room type in draft mode -> 201 Created
        // ---------------------------------------------------------------------
        string newRoomTypeName = $"Royal Villa {Guid.NewGuid().ToString()[..6]}";
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/room-types")
        {
            Content = new StringContent($"{{\"name\":\"{newRoomTypeName}\",\"pricePerNight\":750.00}}", Encoding.UTF8, "application/json")
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var createResp = await _client.SendAsync(createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var createJson = await createResp.Content.ReadAsStringAsync();
        using var createDoc = System.Text.Json.JsonDocument.Parse(createJson);
        var newRoomTypeId = createDoc.RootElement.GetProperty("Id").GetString();
        newRoomTypeId.Should().NotBeNullOrWhiteSpace();

        // ---------------------------------------------------------------------
        // Step 7: Verify Customer cannot see newly created draft room type
        // ---------------------------------------------------------------------
        var verifyDraftReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/room-types");
        verifyDraftReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var verifyDraftResp = await _client.SendAsync(verifyDraftReq);
        var draftContent = await verifyDraftResp.Content.ReadAsStringAsync();
        draftContent.Should().NotContain(newRoomTypeName, "draft room type must not be visible to customer before publishing");

        // ---------------------------------------------------------------------
        // Step 8: Admin publishes the room type -> 200 OK
        // ---------------------------------------------------------------------
        var publishReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/room-types/{newRoomTypeId}/publish");
        publishReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var publishResp = await _client.SendAsync(publishReq);
        publishResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // ---------------------------------------------------------------------
        // Step 9: Verify public customer can NOW see the published room type!
        // ---------------------------------------------------------------------
        var verifyPublishedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/room-types");
        verifyPublishedReq.Headers.Add("X-Forwarded-For", "192.168.1.200");
        var verifyPublishedResp = await _client.SendAsync(verifyPublishedReq);
        verifyPublishedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var publishedContent = await verifyPublishedResp.Content.ReadAsStringAsync();
        publishedContent.Should().Contain(newRoomTypeName, "newly published room type must now be visible to customer");
    }

    [Fact]
    public async Task GetTasks_WithoutToken_Returns401UnauthorizedAtGateway()
    {
        // Act: Call protected /api/v1/tasks without token
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert: Gateway rejects with 401 Unauthorized before reaching FieldOps service
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTasks_WithEmployeeJwt_PassesGatewayToFieldOps()
    {
        // Arrange: Issue employee JWT
        var token = _factory.JwtHelper.GenerateToken(sub: "emp-fieldops-001", role: "Manager");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tasks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act: Call protected /api/v1/tasks through Gateway
        var response = await _client.SendAsync(request);

        // Assert: Gateway authenticates and routes successfully to FieldOps
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BiometricPunch_WithoutToken_AllowedThroughGatewayToFieldOps()
    {
        // Act: Call anonymous /api/v1/attendance/punch without JWT Bearer token
        var content = new StringContent("{\"employeeId\":\"" + Guid.NewGuid() + "\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/attendance/punch", content);

        // Assert: Gateway allows through as Anonymous (device key is checked downstream in FieldOps)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

