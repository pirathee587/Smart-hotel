using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.API.Controllers;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Identity.IntegrationTests;

public class OwnerApprovalAndSelfApprovalTests
{
    [Fact]
    public async Task Owner_CanViewAllApprovalRequests()
    {
        await using var context = CreateContext();
        var req1 = new ApprovalRequest
        {
            Type = "RolePromotion",
            Description = "Promote Alice to Manager",
            RequestedBy = "manager@smarthotel.com",
            RequestedByUserId = Guid.NewGuid()
        };
        var req2 = new ApprovalRequest
        {
            Type = "DepartmentBudget",
            Description = "Increase Housekeeping budget",
            RequestedBy = "admin@smarthotel.com",
            RequestedByUserId = Guid.NewGuid()
        };
        context.ApprovalRequests.AddRange(req1, req2);
        await context.SaveChangesAsync();

        var ownerId = Guid.NewGuid();
        var controller = CreateController(context, ownerId, "owner@smarthotel.com", "Owner");

        var result = await controller.GetAll(default);
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Which;
        var list = okResult.Value.Should().BeAssignableTo<IReadOnlyList<ApprovalRequest>>().Which;

        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task Owner_CanApproveRequestSubmittedByAnotherUser()
    {
        await using var context = CreateContext();
        var requesterId = Guid.NewGuid();
        var approval = new ApprovalRequest
        {
            Type = "RolePromotion",
            Description = "Promote Bob to Manager",
            RequestedBy = "admin@smarthotel.com",
            RequestedByUserId = requesterId,
            Status = "PendingApproval"
        };
        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync();

        var ownerId = Guid.NewGuid(); // different from requesterId
        var controller = CreateController(context, ownerId, "owner@smarthotel.com", "Owner");

        var result = await controller.Approve(approval.Id, default);
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Which;
        var updated = okResult.Value.Should().BeOfType<ApprovalRequest>().Which;

        updated.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Owner_CannotSelfApproveOwnRequest_ByUserId()
    {
        await using var context = CreateContext();
        var ownerId = Guid.NewGuid();
        var approval = new ApprovalRequest
        {
            Type = "ExecutiveExpense",
            Description = "Executive travel reimbursement",
            RequestedBy = "owner@smarthotel.com",
            RequestedByUserId = ownerId,
            Status = "PendingApproval"
        };
        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync();

        var controller = CreateController(context, ownerId, "owner@smarthotel.com", "Owner");

        var result = await controller.Approve(approval.Id, default);
        result.Result.Should().BeOfType<ConflictObjectResult>();

        // Verify status remains PendingApproval in DB
        var reloaded = await context.ApprovalRequests.FindAsync(approval.Id);
        reloaded!.Status.Should().Be("PendingApproval");
    }

    [Fact]
    public async Task Owner_CannotSelfApproveOwnRequest_ByEmailFallback()
    {
        await using var context = CreateContext();
        var approval = new ApprovalRequest
        {
            Type = "ExecutiveExpense",
            Description = "Legacy request without UserId",
            RequestedBy = "owner@smarthotel.com",
            RequestedByUserId = null,
            Status = "PendingApproval"
        };
        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync();

        var ownerId = Guid.NewGuid();
        var controller = CreateController(context, ownerId, "owner@smarthotel.com", "Owner");

        var result = await controller.Approve(approval.Id, default);
        result.Result.Should().BeOfType<ConflictObjectResult>();

        var reloaded = await context.ApprovalRequests.FindAsync(approval.Id);
        reloaded!.Status.Should().Be("PendingApproval");
    }

    [Fact]
    public async Task CreateApprovalRequest_PersistsImmutableUserIdAndEmail()
    {
        await using var context = CreateContext();
        var managerId = Guid.NewGuid();
        var controller = CreateController(context, managerId, "manager@smarthotel.com", "Manager");

        var result = await controller.Create(new CreateApprovalRequest("NewHire", "Hire additional housekeeper"), default);
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Which;
        var created = createdResult.Value.Should().BeOfType<ApprovalRequest>().Which;

        created.RequestedByUserId.Should().Be(managerId);
        created.RequestedBy.Should().Be("manager@smarthotel.com");
        created.Status.Should().Be("PendingApproval");
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ApprovalRequestsController CreateController(AppDbContext context, Guid userId, string email, string role)
    {
        var controller = new ApprovalRequestsController(context);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Email, email),
            new("email", email),
            new(ClaimTypes.Role, role),
            new("role", role)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
        return controller;
    }
}
