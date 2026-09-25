using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.API.Controllers;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;

namespace SmartHotel.Identity.IntegrationTests;

public class DepartmentManagerRuleTests
{
    [Fact]
    public async Task Admin_CanReadOnlyAssignedDepartment()
    {
        await using var context = CreateContext();
        var own = new Department { Name = "Housekeeping" };
        var other = new Department { Name = "Front Office" };
        context.AddRange(own, other);
        await context.SaveChangesAsync();

        var result = await CreateController(context, "Admin", own.Id).GetAll(default);
        var departments = result.Result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeAssignableTo<IEnumerable<DepartmentDto>>().Which;

        departments.Should().ContainSingle(d => d.Id == own.Id);
    }

    [Fact]
    public async Task Admin_CannotUpdateAnotherDepartment()
    {
        await using var context = CreateContext();
        var own = new Department { Name = "Housekeeping" };
        var other = new Department { Name = "Front Office" };
        context.AddRange(own, other);
        await context.SaveChangesAsync();

        var result = await CreateController(context, "Admin", own.Id)
            .Update(other.Id, new SaveDepartmentRequest(other.Name, "blocked", null), default);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SaveChanges_ComputesConcurrencySafeActiveRoleSlot()
    {
        await using var context = CreateContext();
        var department = new Department { Name = "Maintenance" };
        var admin = new Employee { FirstName = "A", LastName = "Admin", Email = "admin-slot@test.local", Role = EmployeeRole.Admin, DepartmentId = department.Id, Status = EmployeeStatus.Active, IsActive = true };
        context.AddRange(department, admin);

        await context.SaveChangesAsync();

        admin.DepartmentRoleSlot.Should().Be($"Admin:{department.Id:D}");
        admin.IsActive = false;
        await context.SaveChangesAsync();
        admin.DepartmentRoleSlot.Should().BeNull();
    }

    [Fact]
    public async Task Update_RejectsEmployeeWithoutManagerRole()
    {
        await using var context = CreateContext();
        var department = new Department { Name = "Front Desk" };
        var employee = new Employee { FirstName = "A", LastName = "Staff", Email = "staff@test.local", Role = EmployeeRole.Receptionist, DepartmentId = department.Id };
        context.AddRange(department, employee);
        await context.SaveChangesAsync();

        var result = await CreateController(context).Update(department.Id, new SaveDepartmentRequest(department.Name, "", employee.Id), default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_RejectsManagerAlreadyAssignedToAnotherDepartment()
    {
        await using var context = CreateContext();
        var first = new Department { Name = "Front Desk" };
        var second = new Department { Name = "Housekeeping" };
        var manager = new Employee { FirstName = "M", LastName = "One", Email = "manager@test.local", Role = EmployeeRole.Manager, DepartmentId = first.Id };
        first.ManagerId = manager.Id;
        context.AddRange(first, second, manager);
        await context.SaveChangesAsync();

        var result = await CreateController(context).Update(second.Id, new SaveDepartmentRequest(second.Name, "", manager.Id), default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_AssignsEligibleManagerAndMovesThemIntoDepartment()
    {
        await using var context = CreateContext();
        var source = new Department { Name = "Unassigned" };
        var target = new Department { Name = "Maintenance" };
        var manager = new Employee { FirstName = "M", LastName = "Two", Email = "manager2@test.local", Role = EmployeeRole.Manager, DepartmentId = source.Id };
        context.AddRange(source, target, manager);
        await context.SaveChangesAsync();

        var result = await CreateController(context).Update(target.Id, new SaveDepartmentRequest(target.Name, "", manager.Id), default);

        result.Result.Should().BeOfType<OkObjectResult>();
        target.ManagerId.Should().Be(manager.Id);
        manager.DepartmentId.Should().Be(target.Id);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DepartmentsController CreateController(AppDbContext context)
        => CreateController(context, "Owner", null);

    private static DepartmentsController CreateController(AppDbContext context, string role, Guid? departmentId)
    {
        var controller = new DepartmentsController(context);
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (departmentId.HasValue) claims.Add(new Claim("departmentId", departmentId.Value.ToString()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };
        return controller;
    }
}
