using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.Identity.Application.Features.Auth.Models;
using SmartHotel.Identity.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Identity.IntegrationTests;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetJwks_ReturnsValidJwksDocument()
    {
        var response = await _client.GetAsync("/.well-known/jwks.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"keys\"");
        content.Should().Contain("\"kty\":\"RSA\"");
        content.Should().Contain("\"alg\":\"RS256\"");
    }

    [Fact]
    public async Task SeededAdmin_CanLoginImmediately()
    {
        var loginRequest = new EmployeeLoginRequest
        {
            Email = DataSeeder.AdminEmail,
            Password = DataSeeder.AdminDefaultPassword
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/employee/login", loginRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResult.Should().NotBeNull();
        loginResult!.AccessToken.Should().NotBeNullOrWhiteSpace();
        loginResult.MustChangePassword.Should().BeFalse();
        loginResult.User.Role.Should().Be("Admin");
        loginResult.User.Email.Should().Be(DataSeeder.AdminEmail);
    }

    [Fact]
    public async Task MustChangePassword_ScopedJwt_BlocksOtherEndpoints_AndAllowsPasswordChange()
    {
        // 1. Log in with employee who has MustChangePassword = true
        var loginRequest = new EmployeeLoginRequest
        {
            Email = "firstday.staff@smarthotel.internal",
            Password = "InitialTempPass123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/employee/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginData.Should().NotBeNull();
        loginData!.MustChangePassword.Should().BeTrue("Employee must be prompted to change password");
        var restrictedToken = loginData.AccessToken;

        // 2. Attempt to access a protected endpoint (/api/v1/auth/me) with restricted token
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedToken);

        var meResponse = await _client.SendAsync(meRequest);

        // Downstream authorization MUST reject other endpoints with 403 Forbidden!
        meResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Restricted MustChangePassword token must block other endpoints");

        var forbiddenBody = await meResponse.Content.ReadAsStringAsync();
        forbiddenBody.Should().Contain("Password change required");

        // 3. Call change-password endpoint with the restricted token
        var changePasswordRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password");
        changePasswordRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedToken);
        changePasswordRequest.Content = JsonContent.Create(new ChangePasswordRequest
        {
            CurrentPassword = "", // empty allowed under MustChangePassword scope
            NewPassword = "Perm@nentSecurePass2026!"
        });

        var changeResponse = await _client.SendAsync(changePasswordRequest);
        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Log in with new password
        var newLoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/employee/login", new EmployeeLoginRequest
        {
            Email = "firstday.staff@smarthotel.internal",
            Password = "Perm@nentSecurePass2026!"
        });
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newLoginData = await newLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        newLoginData!.MustChangePassword.Should().BeFalse();

        // 5. Normal endpoint (/api/v1/auth/me) now accessible with the new token
        var fullMeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        fullMeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newLoginData.AccessToken);
        var fullMeResponse = await _client.SendAsync(fullMeRequest);
        fullMeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Customer_FullRegistration_Verification_AndLoginFlow()
    {
        var testEmail = $"guest.{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterCustomerRequest
        {
            FirstName = "Carlos",
            LastName = "Santana",
            Email = testEmail,
            Password = "Super#Secure$Hotel99!",
            Nationality = "Mexican",
            PreferredLanguage = "es"
        };

        // 1. Register customer
        var regResponse = await _client.PostAsJsonAsync("/api/v1/customers/register", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var regData = await regResponse.Content.ReadFromJsonAsync<CustomerRegistrationResponse>();
        regData.Should().NotBeNull();
        regData!.Email.Should().Be(testEmail);

        // 2. Attempt login before verification -> should fail with unverified email message
        var prematureLogin = await _client.PostAsJsonAsync("/api/v1/customers/login", new CustomerLoginRequest
        {
            Email = testEmail,
            Password = "Super#Secure$Hotel99!"
        });
        prematureLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var prematureBody = await prematureLogin.Content.ReadAsStringAsync();
        prematureBody.Should().Contain("Email is not verified");

        // 3. Retrieve verification token from DbContext
        string verificationToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = db.Customers.First(c => c.Email == testEmail);
            verificationToken = customer.EmailVerificationToken!;
        }

        verificationToken.Should().NotBeNullOrWhiteSpace();

        // 4. Verify email
        var verifyResponse = await _client.GetAsync($"/api/v1/auth/verify-email?email={testEmail}&token={verificationToken}");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Login after verification
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/customers/login", new CustomerLoginRequest
        {
            Email = testEmail,
            Password = "Super#Secure$Hotel99!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginData!.AccessToken.Should().NotBeNullOrWhiteSpace();
        loginData.User.Role.Should().Be("Customer");

        // 6. Access protected /api/v1/auth/me endpoint
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginData.AccessToken);
        var meResponse = await _client.SendAsync(meRequest);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
