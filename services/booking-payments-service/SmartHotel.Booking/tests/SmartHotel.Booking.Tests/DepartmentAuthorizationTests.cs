using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.Authorization;

namespace SmartHotel.Booking.Tests;

public class DepartmentAuthorizationTests
{
    [Theory]
    [InlineData("Manager", "HOUSEKEEPING")]
    [InlineData("Receptionist", "FINANCE")]
    [InlineData("Housekeeper", "FRONTOFFICE")]
    [InlineData("Guest", "FRONTOFFICE")]
    public async Task FrontOfficeOperations_RejectCrossDepartmentAndPrivilegeEscalation(string role, string department)
    {
        var authorization = CreateAuthorization();
        var result = await authorization.AuthorizeAsync(User(role, department), null, HotelPolicies.FrontOfficeOperations);
        result.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData("Receptionist")]
    [InlineData("Manager")]
    [InlineData("Admin")]
    public async Task FrontOfficeOperations_AcceptsConcreteFrontOfficeRoles(string role)
    {
        var result = await CreateAuthorization().AuthorizeAsync(User(role, "FRONTOFFICE"), null, HotelPolicies.FrontOfficeOperations);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task MissingDepartmentId_RejectsOtherwiseMatchingClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "Manager"), new Claim("departmentCode", "FRONTOFFICE")
        }, "test"));
        var result = await CreateAuthorization().AuthorizeAsync(principal, null, HotelPolicies.FrontOfficeOperations);
        result.Succeeded.Should().BeFalse();
    }

    private static IAuthorizationService CreateAuthorization() => new ServiceCollection()
        .AddLogging().AddHotelDepartmentAuthorization().BuildServiceProvider().GetRequiredService<IAuthorizationService>();

    private static ClaimsPrincipal User(string role, string department) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Role, role), new Claim("departmentId", Guid.NewGuid().ToString()),
        new Claim("departmentCode", department)
    }, "test"));
}
