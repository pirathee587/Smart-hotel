using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.API.Controllers;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.API.Services;

namespace SmartHotel.Booking.Tests;

public sealed class SalaryPayrollTests
{
    [Fact]
    public async Task OnlyOwner_CanConfigureDifferentDepartmentRoleSalaries()
    {
        await using var db = Context(); var department = Guid.NewGuid();
        var denied = await Controller(db, Guid.NewGuid(), "Manager", "FINANCE").SaveStructure(new(department, "Manager", 200000m, 2000m, "LKR", new DateOnly(2026, 9, 1)), default);
        denied.Should().BeOfType<ForbidResult>();
        var owner = Controller(db, Guid.NewGuid(), "Owner");
        (await owner.SaveStructure(new(department, "Manager", 200000m, 2000m, "LKR", new DateOnly(2026, 9, 1)), default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        (await owner.SaveStructure(new(department, "Employee", 100000m, 1000m, "LKR", new DateOnly(2026, 9, 1)), default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        (await db.SalaryStructures.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Calculation_UsesOverrideAllowancesAndStoresImmutableSnapshot()
    {
        await using var db = Context(); var finance = Guid.NewGuid(); var employee = Guid.NewGuid(); var department = Guid.NewGuid();
        db.AddRange(
            Grant(finance, FinancePermissions.SubmitPayroll),
            new SalaryStructure { DepartmentId = department, EmployeeRole = "Employee", MonthlyBasicSalary = 100000m, OvertimeHourlyRate = 1000m, Currency = "LKR", EffectiveFrom = new(2026, 1, 1), ApprovedByOwnerId = Guid.NewGuid() },
            new EmployeeSalaryOverride { EmployeeId = employee, MonthlyBasicSalary = 120000m, OvertimeHourlyRate = 1500m, Currency = "LKR", EffectiveFrom = new(2026, 1, 1), Reason = "Senior specialist", ApprovedByOwnerId = Guid.NewGuid() },
            new SalaryAllowanceConfiguration { DepartmentId = department, EmployeeRole = "Employee", Kind = AllowanceKind.Transport, CalculationType = AllowanceCalculationType.FixedAmount, Value = 5000m, Currency = "LKR", EffectiveFrom = new(2026, 1, 1), ApprovedByOwnerId = Guid.NewGuid() },
            new SalaryAllowanceConfiguration { DepartmentId = department, EmployeeRole = "Employee", Kind = AllowanceKind.Attendance, CalculationType = AllowanceCalculationType.PercentageOfBasic, Value = 10m, Currency = "LKR", EffectiveFrom = new(2026, 1, 1), ApprovedByOwnerId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var attendance = VerifiedAttendance(2m);
        var deductions = new[] { new PayrollDeductionInput("Approved unpaid leave", 1000m, "owner-rule-2026-01", "legal-review-2026-01") };
        var result = await Controller(db, finance, "Employee", "FINANCE").Calculate(new(attendance.SummaryId, employee, department, "Employee", new(2026, 8, 1), new(2026, 8, 31), 2m, 1000m, true, attendance, deductions), default);
        var payroll = (FinancePayrollRecord)((ObjectResult)result).Value!;
        payroll.BaseSalary.Should().Be(120000m); payroll.Allowances.Should().Be(17000m); payroll.OvertimePay.Should().Be(3000m); payroll.GrossSalary.Should().Be(140000m); payroll.NetSalary.Should().Be(139000m);
        payroll.CalculationSnapshotJson.Should().Contain("120000").And.Contain("AllowanceTotal");
        payroll.AttendanceSnapshotJson.Should().Contain("ApprovedPaidLeaveDays").And.Contain("WeekendDays");
        db.SalaryStructures.Single().MonthlyBasicSalary = 999999m; await db.SaveChangesAsync();
        payroll.CalculationSnapshotJson.Should().Contain("120000").And.NotContain("999999");
    }

    [Fact]
    public async Task AttendanceDiscrepancies_BlockPayrollWithoutAutomaticDeduction()
    {
        await using var db = Context(); var finance = Guid.NewGuid(); var employee = Guid.NewGuid(); var department = Guid.NewGuid();
        db.AddRange(Grant(finance, FinancePermissions.SubmitPayroll), new SalaryStructure { DepartmentId = department, EmployeeRole = "Employee", MonthlyBasicSalary = 100000m, OvertimeHourlyRate = 1000m, Currency = "LKR", EffectiveFrom = new(2026, 1, 1), ApprovedByOwnerId = Guid.NewGuid() }); await db.SaveChangesAsync();
        var attendance = VerifiedAttendance(0m) with { MissingPunches = 1, WeekendDays = 8, HolidayDays = 1, ApprovedPaidLeaveDays = 2 };
        var result = await Controller(db, finance, "Employee", "FINANCE").Calculate(new(attendance.SummaryId, employee, department, "Employee", new(2026, 8, 1), new(2026, 8, 31), 0m, 0m, true, attendance, Array.Empty<PayrollDeductionInput>()), default);
        result.Should().BeOfType<ConflictObjectResult>(); (await db.FinancePayrollRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MockPayment_RequiresOwnerApproval_AndIsIdempotent()
    {
        await using var db = Context(); var executor = Guid.NewGuid(); db.Add(Grant(executor, FinancePermissions.ExecutePayments));
        var payroll = Payroll(Guid.NewGuid(), PayrollApprovalStatus.OwnerApproval); db.Add(payroll); await db.SaveChangesAsync();
        var controller = Controller(db, executor, "Employee", "FINANCE");
        (await controller.ProcessMockPayment(payroll.Id, new("attempt-1", MockPaymentScenario.Success), default)).Should().BeOfType<ConflictObjectResult>();
        payroll.Status = PayrollApprovalStatus.Approved; payroll.OwnerApprovedByUserId = Guid.NewGuid(); await db.SaveChangesAsync();
        var first = (OkObjectResult)await controller.ProcessMockPayment(payroll.Id, new("attempt-1", MockPaymentScenario.Success), default);
        var second = (OkObjectResult)await controller.ProcessMockPayment(payroll.Id, new("attempt-1", MockPaymentScenario.Success), default);
        first.Value.Should().BeSameAs(second.Value); payroll.Status.Should().Be(PayrollApprovalStatus.Disbursed);
        (await db.MockPayrollPayments.CountAsync()).Should().Be(1); db.MockPayrollPayments.Single().Status.Should().Be(MockPayrollPaymentStatus.MockPaid);
    }

    [Fact]
    public async Task EmployeeSelfService_ReturnsOnlyAuthenticatedEmployeesRecords()
    {
        await using var db = Context(); var employee = Guid.NewGuid(); var another = Guid.NewGuid(); db.AddRange(Payroll(employee, PayrollApprovalStatus.Approved), Payroll(another, PayrollApprovalStatus.Approved)); await db.SaveChangesAsync();
        var result = (OkObjectResult)await Controller(db, employee, "Employee", "HOUSEKEEPING").MySalary(default);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain(employee.ToString()).And.NotContain(another.ToString());
    }

    private static FinancePayrollRecord Payroll(Guid employee, PayrollApprovalStatus status) => new() { SourcePayrollId = Guid.NewGuid(), EmployeeId = employee, DepartmentId = Guid.NewGuid(), EmployeeRole = "Employee", PeriodStart = new(2026, 8, 1), PeriodEnd = new(2026, 8, 31), BaseSalary = 100000m, GrossSalary = 100000m, NetSalary = 100000m, Currency = "LKR", Status = status, SubmittedByUserId = Guid.NewGuid(), CalculationSnapshotJson = "{}" };
    private static FinanceAccessGrant Grant(Guid user, string permission) => new() { UserId = user, Permission = permission, GrantedByUserId = Guid.NewGuid(), IsActive = true };
    private static AttendancePayrollSnapshotRequest VerifiedAttendance(decimal overtime)
    {
        var id = Guid.NewGuid();
        return new(id, 1, 22, 20, 8, 1, 1, 0, overtime, 0, 0, Guid.NewGuid(), DateTime.UtcNow, new[] { Guid.NewGuid() }, Array.Empty<Guid>());
    }
    private static BookingDbContext Context() => new(new DbContextOptionsBuilder<BookingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static SalaryPayrollController Controller(BookingDbContext db, Guid user, string role, string? department = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.ToString()), new(ClaimTypes.Role, role) };
        if (department is not null) claims.Add(new("departmentCode", department));
        return new SalaryPayrollController(db, new AllowAttendanceVerifier()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } } };
    }
    private sealed class AllowAttendanceVerifier : IAttendanceSummaryVerifier { public Task<bool> IsCurrentVerifiedAsync(Guid id, Guid employeeId, Guid departmentId, int year, int month, int version, decimal overtimeHours, string? authorization, CancellationToken ct) => Task.FromResult(true); }
}
