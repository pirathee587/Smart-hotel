using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.Application.Common.Security;

public static class EmployeeRoleHierarchy
{
    private static readonly HashSet<EmployeeRole> EmployeeRoles =
    [
        EmployeeRole.Receptionist,
        EmployeeRole.Housekeeper,
        EmployeeRole.Maintenance,
        EmployeeRole.Chef,
        EmployeeRole.Waiter,
        EmployeeRole.Security
    ];

    public static bool CanCreate(EmployeeRole creator, EmployeeRole target) => creator switch
    {
        EmployeeRole.Owner => target == EmployeeRole.Admin,
        EmployeeRole.Admin => target == EmployeeRole.Manager,
        EmployeeRole.Manager => EmployeeRoles.Contains(target),
        _ => false
    };

    public static EmployeeRole DefaultCreatedRole(EmployeeRole creator) => creator switch
    {
        EmployeeRole.Owner => EmployeeRole.Admin,
        EmployeeRole.Admin => EmployeeRole.Manager,
        EmployeeRole.Manager => EmployeeRole.Receptionist,
        _ => throw new ArgumentOutOfRangeException(nameof(creator), creator, "Role cannot create employees.")
    };
}
