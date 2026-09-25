using FluentAssertions;
using SmartHotel.Identity.Domain.Common;

namespace SmartHotel.Identity.UnitTests;

public class RolePermissionsTests
{
    [Theory]
    [InlineData("Housekeeper")]
    [InlineData("Maintenance")]
    [InlineData("Chef")]
    [InlineData("Waiter")]
    [InlineData("Security")]
    public void OperationalRoles_DoNotReceiveBookingOrRoomManagementPermissions(string role)
    {
        var permissions = RolePermissions.GetPermissionsForRole(role);
        permissions.Should().Contain(RolePermissions.ViewAssignedTasks);
        permissions.Should().NotContain(RolePermissions.ManageBookings);
        permissions.Should().NotContain(RolePermissions.ManageRooms);
        permissions.Should().NotContain(RolePermissions.GuestCheckInCheckOut);
    }

    [Fact]
    public void Receptionist_ReceivesFrontOfficePermissionsWithoutSystemAdministration()
    {
        var permissions = RolePermissions.GetPermissionsForRole("Receptionist");
        permissions.Should().Contain(RolePermissions.GuestCheckInCheckOut);
        permissions.Should().Contain(RolePermissions.ManageBookings);
        permissions.Should().NotContain(RolePermissions.ManageRoles);
        permissions.Should().NotContain(RolePermissions.SystemSettings);
    }

    [Fact]
    public void UnknownRole_ReceivesNoPermissions()
    {
        RolePermissions.GetPermissionsForRole("invented-role").Should().BeEmpty();
    }
}
