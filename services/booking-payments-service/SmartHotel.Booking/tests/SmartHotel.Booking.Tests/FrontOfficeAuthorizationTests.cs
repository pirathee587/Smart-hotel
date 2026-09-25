using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.Authorization;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class FrontOfficeAuthorizationTests
{
    private static IAuthorizationService CreateAuthorization() => new ServiceCollection()
        .AddLogging()
        .AddHotelDepartmentAuthorization()
        .BuildServiceProvider()
        .GetRequiredService<IAuthorizationService>();

    private static ClaimsPrincipal User(string role, string department) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Role, role),
        new Claim("departmentId", Guid.NewGuid().ToString()),
        new Claim("departmentCode", department)
    }, "test"));

    [Theory]
    [InlineData("Receptionist", "FRONTOFFICE", true)]
    [InlineData("Owner", "FRONTOFFICE", true)]
    [InlineData("Manager", "FRONTOFFICE", true)]
    [InlineData("Admin", "FRONTOFFICE", true)]
    [InlineData("Housekeeper", "HOUSEKEEPING", false)]
    [InlineData("Maintenance", "MAINTENANCE", false)]
    [InlineData("Accountant", "FINANCE", false)]
    [InlineData("Guest", "GUEST", false)]
    public async Task FrontOfficeOperations_Policy_AllowsOnlyFrontOfficePersonnel(string role, string department, bool shouldSucceed)
    {
        var auth = CreateAuthorization();
        var result = await auth.AuthorizeAsync(User(role, department), null, HotelPolicies.FrontOfficeOperations);
        result.Succeeded.Should().Be(shouldSucceed);
    }

    [Theory]
    [InlineData("Manager", "FRONTOFFICE", true)]
    [InlineData("Admin", "FRONTOFFICE", true)]
    [InlineData("Receptionist", "FRONTOFFICE", false)] // Regular receptionist cannot override cancellation!
    [InlineData("FrontDesk", "FRONTOFFICE", false)]
    [InlineData("Manager", "HOUSEKEEPING", false)] // Other department managers cannot override front office!
    [InlineData("Manager", "MAINTENANCE", false)]
    public async Task FrontOfficeManagement_Policy_RequiresFrontOfficeManagerOrAdmin(string role, string department, bool shouldSucceed)
    {
        var auth = CreateAuthorization();
        var result = await auth.AuthorizeAsync(User(role, department), null, HotelPolicies.FrontOfficeManagement);
        result.Succeeded.Should().Be(shouldSucceed);
    }
}
