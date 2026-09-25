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

public sealed class FinanceOperationsTests
{
    [Theory]
    [InlineData(InvoiceType.Customer)]
    [InlineData(InvoiceType.Supplier)]
    public async Task Invoice_FollowsValidLifecycle_AndGeneratesPdf(InvoiceType type)
    {
        await using var db = Context(); var controller = Controller(db, Guid.NewGuid(), "Employee", "FINANCE", "Accountant");
        var created = (ObjectResult)await controller.CreateInvoice(new CreateInvoiceRequest(type, $"charge-{type}", null, null, "Party", "party@example.test", 100m, 10m, "LKR", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))), default);
        var invoice = (FinanceInvoice)created.Value!;
        await controller.InvoiceAction(invoice.Id, new InvoiceActionRequest("validate"), default);
        await controller.InvoiceAction(invoice.Id, new InvoiceActionRequest("issue"), default);
        await controller.InvoiceAction(invoice.Id, new InvoiceActionRequest("record-payment", 40m), default);
        invoice.Status.Should().Be(InvoiceStatus.PartiallyPaid);
        await controller.InvoiceAction(invoice.Id, new InvoiceActionRequest("record-payment", 70m), default);
        invoice.Status.Should().Be(InvoiceStatus.Settled);
        var pdf = (FileContentResult)await controller.InvoicePdf(invoice.Id, default);
        pdf.ContentType.Should().Be("application/pdf"); pdf.FileContents.Take(4).Should().Equal("%PDF"u8.ToArray());
    }

    [Fact]
    public async Task DuplicateCharge_CannotCreateDuplicateInvoice()
    {
        await using var db = Context(); var accountant = Guid.NewGuid(); var controller = Controller(db, accountant, "Employee", "FINANCE", "Accountant");
        var request = new CreateInvoiceRequest(InvoiceType.Customer, "booking-charge-1", null, null, "Guest", "guest@example.test", 100m, 18m, "LKR", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));
        (await controller.CreateInvoice(request, default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        (await controller.CreateInvoice(request, default)).Should().BeOfType<OkObjectResult>();
        (await db.FinanceInvoices.CountAsync()).Should().Be(1);
        (await db.FinanceAuditLogs.CountAsync(a => a.Action == "invoice.created")).Should().Be(1);
    }

    [Fact]
    public async Task NonFinanceDepartment_CannotAccessTransactionsOrInvoices()
    {
        await using var db = Context(); var controller = Controller(db, Guid.NewGuid(), "Manager", "HOUSEKEEPING", null);
        (await controller.Transactions(null, null, null, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.Invoices(null, default)).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Payroll_RequiresVerificationManagerAndOwnerStages()
    {
        await using var db = Context();
        var submitter = Guid.NewGuid(); var verifier = Guid.NewGuid(); var manager = Guid.NewGuid(); var owner = Guid.NewGuid();
        db.AddRange(Grant(submitter, FinancePermissions.SubmitPayroll), Grant(verifier, FinancePermissions.ViewPayroll), Grant(manager, FinancePermissions.ApprovePayroll)); await db.SaveChangesAsync();
        var import = new ImportPayrollRequest(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), 1000m, 100m, 50m, "LKR");
        var created = await Controller(db, submitter, "Employee", "FINANCE", "Accountant").ImportPayroll(import, default);
        var payroll = (FinancePayrollRecord)((ObjectResult)created).Value!;
        await Controller(db, verifier, "Employee", "FINANCE", "Finance Assistant").PayrollDecision(payroll.Id, new PayrollDecisionRequest(true, null), default);
        payroll.Status.Should().Be(PayrollApprovalStatus.ManagerReview);
        await Controller(db, manager, "Manager", "FINANCE", null).PayrollDecision(payroll.Id, new PayrollDecisionRequest(true, null), default);
        payroll.Status.Should().Be(PayrollApprovalStatus.OwnerApproval);
        await Controller(db, owner, "Owner", null, null).PayrollDecision(payroll.Id, new PayrollDecisionRequest(true, null), default);
        payroll.Status.Should().Be(PayrollApprovalStatus.Approved);
        payroll.OwnerApprovedByUserId.Should().Be(owner);
    }

    [Fact]
    public async Task Reconciliation_WithDifference_CannotBeCompleted()
    {
        await using var db = Context(); var employee = Guid.NewGuid(); var manager = Guid.NewGuid();
        db.AddRange(Grant(employee, FinancePermissions.ReconcileSettlements), Grant(manager, FinancePermissions.ReconcileSettlements));
        var payment = new Payment { BookingId = Guid.NewGuid(), Amount = 100m, Currency = "LKR", PayHereOrderId = Guid.NewGuid().ToString(), Status = PaymentStatus.Settled };
        var settlement = new SettlementRecord { PaymentId = payment.Id, Amount = 100m, Currency = "LKR", Status = SettlementStatus.Confirmed, ProviderSettlementReference = Guid.NewGuid().ToString() };
        db.AddRange(payment, settlement); await db.SaveChangesAsync();
        var result = await Controller(db, employee, "Employee", "FINANCE", "Accountant").Reconcile(new ReconciliationRequest(settlement.Id, 100m, 2m, 97m, "LKR", null), default);
        var rec = (SettlementReconciliation)((ObjectResult)result).Value!; rec.DifferenceAmount.Should().Be(-1m);
        (await Controller(db, manager, "Manager", "FINANCE", null).CompleteReconciliation(rec.Id, default)).Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ApprovedPayroll_IsNotFalselyMarkedDisbursedWithoutProvider()
    {
        await using var db = Context(); var executor = Guid.NewGuid(); db.Add(Grant(executor, FinancePermissions.ExecutePayments));
        var payroll = new FinancePayrollRecord { SourcePayrollId = Guid.NewGuid(), EmployeeId = Guid.NewGuid(), PeriodStart = new DateOnly(2026, 8, 1), PeriodEnd = new DateOnly(2026, 8, 31), BaseSalary = 1000m, NetSalary = 1000m, Currency = "LKR", Status = PayrollApprovalStatus.Approved, SubmittedByUserId = Guid.NewGuid() };
        db.Add(payroll); await db.SaveChangesAsync();
        var result = await Controller(db, executor, "Employee", "FINANCE", "Accountant").DisbursePayroll(payroll.Id, default);
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(501); payroll.Status.Should().Be(PayrollApprovalStatus.Approved);
    }

    [Fact]
    public async Task ReportMetricsAndExports_MatchVerifiedDatabaseValues()
    {
        await using var db = Context(); var owner = Guid.NewGuid();
        db.Payments.AddRange(
            new Payment { BookingId = Guid.NewGuid(), Amount = 100m, RefundedAmount = 20m, Currency = "LKR", PayHereOrderId = Guid.NewGuid().ToString(), Status = PaymentStatus.PartiallyRefunded },
            new Payment { BookingId = Guid.NewGuid(), Amount = 50m, Currency = "LKR", PayHereOrderId = Guid.NewGuid().ToString(), Status = PaymentStatus.FundsHeld },
            new Payment { BookingId = Guid.NewGuid(), Amount = 999m, Currency = "LKR", PayHereOrderId = Guid.NewGuid().ToString(), Status = PaymentStatus.Failed });
        db.ExpenseRequests.Add(new ExpenseRequest { Amount = 30m, Currency = "LKR", Description = "Verified", Category = "Ops", IdempotencyKey = Guid.NewGuid().ToString(), SubmittedByUserId = Guid.NewGuid(), SubmittedByDepartmentId = Guid.NewGuid(), Status = FinanceRequestStatus.Approved });
        db.FinancePayrollRecords.Add(new FinancePayrollRecord { SourcePayrollId = Guid.NewGuid(), EmployeeId = Guid.NewGuid(), PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)), PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow), NetSalary = 40m, Currency = "LKR", Status = PayrollApprovalStatus.Approved, SubmittedByUserId = Guid.NewGuid() });
        await db.SaveChangesAsync(); var controller = Controller(db, owner, "Owner", null, null);
        var report = (OkObjectResult)await controller.Report(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), default);
        var json = System.Text.Json.JsonSerializer.Serialize(report.Value); json.Should().Contain("\"verifiedCollectedRevenue\":150").And.Contain("\"refunds\":20").And.Contain("\"approvedExpenses\":30").And.Contain("\"approvedPayroll\":40").And.Contain("not an accounting profit");
        ((FileContentResult)await controller.Export(default)).ContentType.Should().Be("text/csv");
        ((FileContentResult)await controller.ExportExcel(default)).ContentType.Should().Be("application/vnd.ms-excel");
    }

    private static BookingDbContext Context() => new(new DbContextOptionsBuilder<BookingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static FinanceAccessGrant Grant(Guid user, string permission) => new() { UserId = user, Permission = permission, GrantedByUserId = Guid.NewGuid(), IsActive = true };
    private static FinanceOperationsController Controller(BookingDbContext db, Guid user, string role, string? department, string? designation)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.ToString()), new(ClaimTypes.Role, role) };
        if (department is not null) claims.Add(new("departmentCode", department)); if (designation is not null) claims.Add(new("designation", designation));
        return new FinanceOperationsController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } } };
    }
}
