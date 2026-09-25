using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace SmartHotel.Authorization;

public static class HotelPolicies
{
    public const string FrontOfficeOperations = "Department.FrontOffice.Operations";
    public const string FrontOfficeManagement = "Department.FrontOffice.Management";
    public const string HousekeepingRoomOperations = "Department.Housekeeping.RoomOperations";
    public const string HousekeepingInspection = "Department.Housekeeping.Inspection";
    public const string MaintenanceRoomOperations = "Department.Maintenance.RoomOperations";
    public const string MaintenanceVerification = "Department.Maintenance.Verification";
    public const string FoodAndBeverageOperations = "Department.FoodAndBeverage.Operations";
    public const string FoodAndBeverageManagement = "Department.FoodAndBeverage.Management";
    public const string KitchenOperations = "Department.FoodAndBeverage.KitchenOperations";
    public const string WaitstaffOperations = "Department.FoodAndBeverage.WaitstaffOperations";
}

public sealed record DepartmentRoleRequirement(
    string DepartmentCode,
    IReadOnlySet<string> Roles,
    bool AllowOwner = false) : IAuthorizationRequirement;

public sealed class DepartmentRoleAuthorizationHandler
    : AuthorizationHandler<DepartmentRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DepartmentRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role)
            ?? context.User.FindFirstValue("role");

        if (requirement.AllowOwner && string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var departmentId = context.User.FindFirstValue("departmentId");
        var departmentCode = Normalize(context.User.FindFirstValue("departmentCode"));
        if (Guid.TryParse(departmentId, out var parsedDepartmentId)
            && parsedDepartmentId != Guid.Empty
            && departmentCode == Normalize(requirement.DepartmentCode)
            && role is not null
            && requirement.Roles.Contains(role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static string Normalize(string? value) =>
        new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

public static class DepartmentAuthorizationExtensions
{
    public static IServiceCollection AddHotelDepartmentAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, DepartmentRoleAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(HotelPolicies.FrontOfficeOperations, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "FRONTOFFICE",
                    RoleSet("Admin", "Manager", "Receptionist"),
                    AllowOwner: true)));

            options.AddPolicy(HotelPolicies.FrontOfficeManagement, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "FRONTOFFICE",
                    RoleSet("Admin", "Manager"),
                    AllowOwner: true)));

            options.AddPolicy(HotelPolicies.HousekeepingRoomOperations, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "HOUSEKEEPING",
                    RoleSet("Admin", "Manager", "Housekeeper"),
                    AllowOwner: true)));

            options.AddPolicy(HotelPolicies.HousekeepingInspection, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "HOUSEKEEPING",
                    RoleSet("Admin", "Manager"),
                    AllowOwner: true)));

            options.AddPolicy(HotelPolicies.MaintenanceRoomOperations, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "MAINTENANCE",
                    RoleSet("Admin", "Manager", "Maintenance"),
                    AllowOwner: true)));
            options.AddPolicy(HotelPolicies.MaintenanceVerification, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "MAINTENANCE",
                    RoleSet("Admin", "Manager"),
                    AllowOwner: true)));

            options.AddPolicy(HotelPolicies.FoodAndBeverageOperations, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "FOODBEVERAGE",
                    RoleSet("Admin", "Manager", "Chef", "Waiter"),
                    AllowOwner: true)));

            options.AddPolicy(HotelPolicies.FoodAndBeverageManagement, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "FOODBEVERAGE",
                    RoleSet("Admin", "Manager"),
                    AllowOwner: true)));

            options.AddPolicy(HotelPolicies.KitchenOperations, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "FOODBEVERAGE",
                    RoleSet("Admin", "Manager", "Chef"),
                    AllowOwner: true)));

            options.AddPolicy(HotelPolicies.WaitstaffOperations, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new DepartmentRoleRequirement(
                    "FOODBEVERAGE",
                    RoleSet("Admin", "Manager", "Waiter"),
                    AllowOwner: true)));
        });

        return services;
    }

    private static IReadOnlySet<string> RoleSet(params string[] roles) =>
        new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
}
