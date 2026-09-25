using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.Authorization;

namespace SmartHotel.HotelOps.Tests;

public class RoomAuthorizationTests
{
    [Theory]
    [InlineData("Receptionist", "FRONTOFFICE", HotelPolicies.HousekeepingRoomOperations)]
    [InlineData("Housekeeper", "HOUSEKEEPING", HotelPolicies.MaintenanceRoomOperations)]
    [InlineData("Maintenance", "MAINTENANCE", HotelPolicies.FrontOfficeOperations)]
    [InlineData("Manager", "FINANCE", HotelPolicies.FrontOfficeOperations)]
    public async Task RoomTransitionPolicy_RejectsWrongRoleOrDepartment(string role, string department, string policy)
    {
        var result = await CreateAuthorization().AuthorizeAsync(User(role, department), null, policy);
        result.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData("Receptionist", "FRONTOFFICE", HotelPolicies.FrontOfficeOperations)]
    [InlineData("Housekeeper", "HOUSEKEEPING", HotelPolicies.HousekeepingRoomOperations)]
    [InlineData("Maintenance", "MAINTENANCE", HotelPolicies.MaintenanceRoomOperations)]
    public async Task RoomTransitionPolicy_AcceptsOnlyMatchingOperationalIdentity(string role, string department, string policy)
    {
        var result = await CreateAuthorization().AuthorizeAsync(User(role, department), null, policy);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GenericEmployee_CannotMutateRoomStatus()
    {
        var employee = User("Employee", "FRONTOFFICE");
        var authorization = CreateAuthorization();
        var results = await Task.WhenAll(
            authorization.AuthorizeAsync(employee, null, HotelPolicies.FrontOfficeOperations),
            authorization.AuthorizeAsync(employee, null, HotelPolicies.HousekeepingRoomOperations),
            authorization.AuthorizeAsync(employee, null, HotelPolicies.MaintenanceRoomOperations));
        results.Should().OnlyContain(result => !result.Succeeded);
    }

    [Fact]
    public async Task HousekeeperCannotApproveInspection_ButHousekeepingManagerCan()
    {
        var authorization=CreateAuthorization();
        (await authorization.AuthorizeAsync(User("Housekeeper","HOUSEKEEPING"),null,HotelPolicies.HousekeepingInspection)).Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(User("Manager","HOUSEKEEPING"),null,HotelPolicies.HousekeepingInspection)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task TechnicianCannotClearRestriction_ButMaintenanceManagerCan()
    {
        var authorization=CreateAuthorization();
        (await authorization.AuthorizeAsync(User("Maintenance","MAINTENANCE"),null,HotelPolicies.MaintenanceVerification)).Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(User("Manager","MAINTENANCE"),null,HotelPolicies.MaintenanceVerification)).Succeeded.Should().BeTrue();
        (await authorization.AuthorizeAsync(User("Manager","HOUSEKEEPING"),null,HotelPolicies.MaintenanceVerification)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Owner_HasAuthorizedAccessAcrossDepartmentPolicies()
    {
        var authorization = CreateAuthorization();
        var owner = OwnerUser();
        var policies = new[]
        {
            HotelPolicies.FrontOfficeOperations,
            HotelPolicies.FrontOfficeManagement,
            HotelPolicies.HousekeepingRoomOperations,
            HotelPolicies.HousekeepingInspection,
            HotelPolicies.MaintenanceRoomOperations,
            HotelPolicies.MaintenanceVerification,
            HotelPolicies.FoodAndBeverageOperations,
            HotelPolicies.FoodAndBeverageManagement,
            HotelPolicies.KitchenOperations,
            HotelPolicies.WaitstaffOperations
        };

        foreach (var policy in policies)
        {
            var result = await authorization.AuthorizeAsync(owner, null, policy);
            result.Succeeded.Should().BeTrue($"Owner should be authorized for policy {policy}");
        }
    }

    [Fact]
    public async Task NonOwnerCrossDepartmentAccess_IsDenied()
    {
        var authorization = CreateAuthorization();
        var frontOfficeManager = User("Manager", "FRONTOFFICE");
        var housekeepingStaff = User("Housekeeper", "HOUSEKEEPING");
        var maintenanceStaff = User("Maintenance", "MAINTENANCE");

        (await authorization.AuthorizeAsync(frontOfficeManager, null, HotelPolicies.HousekeepingInspection)).Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(frontOfficeManager, null, HotelPolicies.MaintenanceVerification)).Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(housekeepingStaff, null, HotelPolicies.FrontOfficeManagement)).Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(maintenanceStaff, null, HotelPolicies.FoodAndBeverageManagement)).Succeeded.Should().BeFalse();
    }

    private static IAuthorizationService CreateAuthorization() => new ServiceCollection()
        .AddLogging().AddHotelDepartmentAuthorization().BuildServiceProvider().GetRequiredService<IAuthorizationService>();

    private static ClaimsPrincipal OwnerUser() => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, "Owner"),
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new Claim("sub", Guid.NewGuid().ToString())
    }, "test"));

    private static ClaimsPrincipal User(string role, string department) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, role), new Claim("departmentId", Guid.NewGuid().ToString()),
        new Claim("departmentCode", department)
    }, "test"));
}
