using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.API.Controllers;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;

namespace SmartHotel.Booking.Tests;

public class FinanceAuthorizationTests
{
    private static readonly Guid FinanceDepartment = Guid.NewGuid();

    [Fact]
    public async Task Owner_CanViewHotelWideFinanceOverview()
    {
        await using var db = Context();
        db.Payments.Add(Payment(250m));
        await db.SaveChangesAsync();

        var result = await Controller(db, Guid.NewGuid(), "Owner").Overview(default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task FinanceAdmin_CannotApproveWithoutFinancialPermission()
    {
        await using var db = Context();
        var submitter = Guid.NewGuid();
        var expense = Expense(submitter, 100m);
        db.AddRange(expense, Settings(500m));
        await db.SaveChangesAsync();

        var result = await Controller(db, Guid.NewGuid(), "Admin", "FINANCE", FinanceDepartment)
            .DecideExpense(expense.Id, new FinanceDecisionRequest(true, null), default);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task FinanceManager_WithGrant_CanApproveWithinConfiguredLimit()
    {
        await using var db = Context();
        var manager = Guid.NewGuid();
        var expense = Expense(Guid.NewGuid(), 100m);
        db.AddRange(expense, Settings(500m), Grant(manager, FinancePermissions.ApproveExpenses));
        await db.SaveChangesAsync();

        var result = await Controller(db, manager, "Manager", "FINANCE", FinanceDepartment)
            .DecideExpense(expense.Id, new FinanceDecisionRequest(true, "Reviewed"), default);

        result.Should().BeOfType<OkObjectResult>();
        expense.Status.Should().Be(FinanceRequestStatus.Approved);
        expense.ApprovalStage.Should().Be(FinanceApprovalStage.Complete);
    }

    [Fact]
    public async Task HighValueExpense_RequiresOwnerFinalDecision()
    {
        await using var db = Context();
        var manager = Guid.NewGuid();
        var expense = Expense(Guid.NewGuid(), 1000m);
        db.AddRange(expense, Settings(500m), Grant(manager, FinancePermissions.ApproveExpenses));
        await db.SaveChangesAsync();

        await Controller(db, manager, "Manager", "FINANCE", FinanceDepartment)
            .DecideExpense(expense.Id, new FinanceDecisionRequest(true, "Escalate"), default);

        expense.Status.Should().Be(FinanceRequestStatus.Pending);
        expense.ApprovalStage.Should().Be(FinanceApprovalStage.Owner);
        var ownerResult = await Controller(db, Guid.NewGuid(), "Owner")
            .DecideExpense(expense.Id, new FinanceDecisionRequest(true, "Final approval"), default);
        ownerResult.Should().BeOfType<OkObjectResult>();
        expense.Status.Should().Be(FinanceRequestStatus.Approved);
    }

    [Fact]
    public async Task FinanceEmployee_CannotApproveOwnExpense()
    {
        await using var db = Context();
        var employee = Guid.NewGuid();
        var expense = Expense(employee, 50m);
        db.AddRange(expense, Settings(500m), Grant(employee, FinancePermissions.ApproveExpenses));
        await db.SaveChangesAsync();

        var result = await Controller(db, employee, "Manager", "FINANCE", FinanceDepartment)
            .DecideExpense(expense.Id, new FinanceDecisionRequest(true, null), default);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task HousekeepingManager_CannotAccessFinanceReports()
    {
        await using var db = Context();
        var result = await Controller(db, Guid.NewGuid(), "Manager", "HOUSEKEEPING", Guid.NewGuid()).Overview(default);
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DuplicateRefundRequest_ReturnsExistingRecord()
    {
        await using var db = Context();
        var employee = Guid.NewGuid();
        var payment = Payment(200m);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        var controller = Controller(db, employee, "Receptionist", "FINANCE", FinanceDepartment, "Finance Assistant");
        var request = new CreateRefundRequest(payment.Id, 50m, "LKR", "Guest cancellation", "refund-1", FinanceDepartment);

        var first = await controller.SubmitRefund(request, default);
        var second = await controller.SubmitRefund(request, default);

        first.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        second.Should().BeOfType<OkObjectResult>();
        (await db.RefundRequests.CountAsync()).Should().Be(1);
    }

    private static BookingDbContext Context() => new(new DbContextOptionsBuilder<BookingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static FinanceController Controller(BookingDbContext db, Guid userId, string role, string? departmentCode = null, Guid? departmentId = null, string? designation = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Role, role), new("role", role) };
        if (departmentCode is not null) claims.Add(new Claim("departmentCode", departmentCode));
        if (departmentId.HasValue) claims.Add(new Claim("departmentId", departmentId.Value.ToString()));
        if (designation is not null) claims.Add(new Claim("designation", designation));
        return new FinanceController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } } };
    }

    private static ExpenseRequest Expense(Guid submitter, decimal amount) => new() { Amount = amount, Currency = "LKR", Description = "Test expense", Category = "Supplies", IdempotencyKey = Guid.NewGuid().ToString(), SubmittedByUserId = submitter, SubmittedByDepartmentId = FinanceDepartment, VerificationStatus = ExpenseVerificationStatus.Verified };
    private static FinanceApprovalSettings Settings(decimal limit) => new() { ManagerExpenseApprovalLimit = limit, ManagerRefundApprovalLimit = limit, Currency = "LKR", UpdatedByUserId = Guid.NewGuid() };
    private static FinanceAccessGrant Grant(Guid user, string permission) => new() { UserId = user, Permission = permission, GrantedByUserId = Guid.NewGuid(), IsActive = true };
    private static Payment Payment(decimal amount) => new() { BookingId = Guid.NewGuid(), Amount = amount, Currency = "LKR", PayHereOrderId = Guid.NewGuid().ToString(), PayHerePaymentId = Guid.NewGuid().ToString(), Status = PaymentStatus.Completed };
}
