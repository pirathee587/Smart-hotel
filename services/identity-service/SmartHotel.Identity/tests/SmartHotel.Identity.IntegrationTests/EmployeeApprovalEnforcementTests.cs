using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.Identity.API.Controllers;
using SmartHotel.Identity.API.Middleware;
using SmartHotel.Identity.Application.Features.Auth.Commands;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;
using SmartHotel.Identity.Infrastructure.Services;

namespace SmartHotel.Identity.IntegrationTests;

public class EmployeeApprovalEnforcementTests
{
    [Fact]
    public async Task Manager_CreatesEmployeeInOwnDepartment_AsPendingApproval()
    {
        await using var db = CreateContext();
        var department = new Department { Name = "Housekeeping" };
        var manager = Manager(department.Id);
        department.ManagerId = manager.Id;
        db.AddRange(department, manager);
        await db.SaveChangesAsync();

        var controller = Controller(db, manager);
        var result = await controller.Create(Request(department.Id), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        (await db.Employees.SingleAsync(e => e.Email == "worker@test.local")).Status.Should().Be(EmployeeStatus.PendingApproval);
    }

    [Fact]
    public async Task Manager_CannotCreateEmployeeInAnotherDepartment()
    {
        await using var db = CreateContext();
        var own = new Department { Name = "Housekeeping" };
        var other = new Department { Name = "Maintenance" };
        var manager = Manager(own.Id);
        own.ManagerId = manager.Id;
        db.AddRange(own, other, manager);
        await db.SaveChangesAsync();

        var result = await Controller(db, manager).Create(Request(other.Id), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await db.Employees.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(EmployeeStatus.Active, true, true)]
    [InlineData(EmployeeStatus.PendingApproval, false, false)]
    [InlineData(EmployeeStatus.Rejected, false, false)]
    [InlineData(EmployeeStatus.Active, false, false)]
    public async Task ProtectedRequest_AllowsOnlyCurrentlyApprovedEmployee(EmployeeStatus status, bool isActive, bool expectedNext)
    {
        await using var db = CreateContext();
        var department = new Department { Name = "Front Desk" };
        var employee = new Employee { FirstName = "A", LastName = "Worker", Email = $"{Guid.NewGuid()}@test.local", Role = EmployeeRole.Receptionist, DepartmentId = department.Id, Status = status, IsActive = isActive };
        db.AddRange(department, employee);
        await db.SaveChangesAsync();
        var nextCalled = false;
        var middleware = new ActiveEmployeeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, employee.Id.ToString()), new Claim(ClaimTypes.Role, "Receptionist")
        ], "test"));

        await middleware.InvokeAsync(context, db);

        nextCalled.Should().Be(expectedNext);
        if (!expectedNext) context.Response.StatusCode.Should().Be(403);
    }

    [Theory]
    [InlineData(EmployeeStatus.PendingApproval, false)]
    [InlineData(EmployeeStatus.Rejected, false)]
    [InlineData(EmployeeStatus.Active, true)]
    public async Task Login_IssuesTokensOnlyToApprovedEmployee(EmployeeStatus status, bool shouldSucceed)
    {
        await using var db = CreateContext();
        var department = new Department { Name = "Front Desk" };
        var hasher = new PasswordHasher();
        var employee = new Employee { FirstName = "Login", LastName = "Test", Email = $"{Guid.NewGuid()}@test.local", PasswordHash = hasher.HashPassword("Correct1!"), Role = EmployeeRole.Receptionist, DepartmentId = department.Id, Status = status, IsActive = status == EmployeeStatus.Active };
        db.AddRange(department, employee);
        await db.SaveChangesAsync();
        var tokens = new RecordingTokenService();
        var handler = new LoginCommandHandler(db, hasher, tokens, new InMemoryRateLimiterService(), NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.HandleAsync(new LoginCommand { Username = employee.Email, Password = "Correct1!" });

        result.Succeeded.Should().Be(shouldSucceed);
        tokens.IssueCount.Should().Be(shouldSucceed ? 1 : 0);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Employee Manager(Guid departmentId) => new() { FirstName = "M", LastName = "Manager", Email = $"{Guid.NewGuid()}@test.local", Role = EmployeeRole.Manager, DepartmentId = departmentId, Status = EmployeeStatus.Active, IsActive = true };
    private static CreateEmployeeRequest Request(Guid departmentId) => new() { FirstName = "New", LastName = "Worker", Email = "worker@test.local", Role = "Housekeeper", DepartmentId = departmentId };
    private static EmployeesController Controller(AppDbContext db, Employee manager)
    {
        var controller = new EmployeesController(db, new PasswordHasher(), new FakeEmailService(NullLogger<FakeEmailService>.Instance));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, manager.Id.ToString()), new Claim(ClaimTypes.Role, "Manager"),
            new Claim("departmentId", manager.DepartmentId.ToString())
        ], "test")) } };
        return controller;
    }

    private sealed class RecordingTokenService : ITokenService
    {
        public int IssueCount { get; private set; }
        public Task<IssuedTokenPair> IssueTokensAsync(Person person, CancellationToken ct = default) { IssueCount++; return Task.FromResult(new IssuedTokenPair("access", "refresh", 3600)); }
        public Task<IssuedTokenPair?> RefreshAsync(string refreshToken, CancellationToken ct = default) => Task.FromResult<IssuedTokenPair?>(null);
        public Task<bool> RevokeAsync(string refreshToken, CancellationToken ct = default) => Task.FromResult(false);
        public Task RevokeAllAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public string GenerateRefreshToken() => "refresh";
    }
}
