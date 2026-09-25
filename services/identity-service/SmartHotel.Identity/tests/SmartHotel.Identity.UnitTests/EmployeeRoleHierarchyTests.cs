using FluentAssertions;
using SmartHotel.Identity.Application.Common.Security;
using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.UnitTests;

public class EmployeeRoleHierarchyTests
{
    [Fact]
    public void Owner_CanCreateAdmin()
    {
        EmployeeRoleHierarchy.CanCreate(EmployeeRole.Owner, EmployeeRole.Admin).Should().BeTrue();
    }

    [Fact]
    public void Admin_CanCreateManager()
    {
        EmployeeRoleHierarchy.CanCreate(EmployeeRole.Admin, EmployeeRole.Manager).Should().BeTrue();
    }

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Admin)]
    public void Admin_CannotCreateOwnerOrAnotherAdmin(EmployeeRole target)
    {
        EmployeeRoleHierarchy.CanCreate(EmployeeRole.Admin, target).Should().BeFalse();
    }

    [Theory]
    [InlineData(EmployeeRole.Receptionist)]
    [InlineData(EmployeeRole.Housekeeper)]
    [InlineData(EmployeeRole.Maintenance)]
    [InlineData(EmployeeRole.Chef)]
    [InlineData(EmployeeRole.Waiter)]
    [InlineData(EmployeeRole.Security)]
    public void Manager_CanCreateExistingEmployeeRoles(EmployeeRole target)
    {
        EmployeeRoleHierarchy.CanCreate(EmployeeRole.Manager, target).Should().BeTrue();
    }

    [Theory]
    [InlineData(EmployeeRole.Owner, EmployeeRole.Admin)]
    [InlineData(EmployeeRole.Admin, EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Manager, EmployeeRole.Receptionist)]
    public void DefaultCreatedRole_FollowsHierarchy(EmployeeRole creator, EmployeeRole expected)
    {
        EmployeeRoleHierarchy.DefaultCreatedRole(creator).Should().Be(expected);
    }
}
