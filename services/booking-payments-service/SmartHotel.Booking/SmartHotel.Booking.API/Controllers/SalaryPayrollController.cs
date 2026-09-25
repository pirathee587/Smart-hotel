using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.API.Services;

namespace SmartHotel.Booking.API.Controllers;

public record SalaryStructureRequest(Guid DepartmentId, string EmployeeRole, decimal MonthlyBasicSalary, decimal OvertimeHourlyRate, string Currency, DateOnly EffectiveFrom);
public record SalaryOverrideRequest(Guid EmployeeId, decimal MonthlyBasicSalary, decimal? OvertimeHourlyRate, string Currency, DateOnly EffectiveFrom, string Reason);
public record AllowanceConfigurationRequest(Guid? DepartmentId, string? EmployeeRole, Guid? EmployeeId, AllowanceKind Kind, AllowanceCalculationType CalculationType, decimal Value, string Currency, DateOnly EffectiveFrom);
public record AttendancePayrollSnapshotRequest(Guid SummaryId, int SummaryVersion, int ScheduledDays, int WorkedDays, int WeekendDays, int HolidayDays, int ApprovedPaidLeaveDays, int ApprovedUnpaidLeaveDays, decimal ApprovedOvertimeHours, int MissingPunches, int DisputedRecords, Guid VerifiedByManagerId, DateTime VerifiedAtUtc, IReadOnlyList<Guid> AttendanceRecordIds, IReadOnlyList<Guid> ApprovedLeaveRecordIds);
public record PayrollDeductionInput(string Label, decimal Amount, string RuleReference, string ValidationReference);
public record CalculatePayrollRequest(Guid SourcePayrollId, Guid EmployeeId, Guid DepartmentId, string EmployeeRole, DateOnly PeriodStart, DateOnly PeriodEnd, decimal ApprovedOvertimeHours, decimal AuthorizedDeductions, bool AttendanceVerified, AttendancePayrollSnapshotRequest? Attendance = null, IReadOnlyList<PayrollDeductionInput>? DeductionItems = null);
public record MockPayrollPaymentRequest(string IdempotencyKey, MockPaymentScenario Scenario);

[ApiController]
[Route("api/v1/salary-payroll")]
[Authorize]
public sealed class SalaryPayrollController : ControllerBase
{
    private readonly BookingDbContext _db;
    private readonly IAttendanceSummaryVerifier _attendanceVerifier;
    public SalaryPayrollController(BookingDbContext db, IAttendanceSummaryVerifier attendanceVerifier) { _db = db; _attendanceVerifier = attendanceVerifier; }

    [HttpGet("structures")]
    public async Task<IActionResult> Structures(CancellationToken ct)
    {
        if (!IsOwner()) return Forbid();
        return Ok(await _db.SalaryStructures.AsNoTracking().OrderBy(s => s.DepartmentId).ThenBy(s => s.EmployeeRole).ThenByDescending(s => s.EffectiveFrom).ToListAsync(ct));
    }

    [HttpPost("structures")]
    public async Task<IActionResult> SaveStructure(SalaryStructureRequest request, CancellationToken ct)
    {
        if (!IsOwner()) return Forbid();
        if (request.DepartmentId == Guid.Empty || !ValidRole(request.EmployeeRole) || request.MonthlyBasicSalary <= 0 || request.OvertimeHourlyRate < 0 || !ValidCurrency(request.Currency)) return BadRequest(new { message = "Department, supported role, positive salary, overtime rate and ISO currency are required." });
        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        using var salaryLock = await _db.AcquirePaymentLockAsync($"salary:{request.DepartmentId}:{request.EmployeeRole.ToUpperInvariant()}", ct);
        var existing = await _db.SalaryStructures.Where(s => s.DepartmentId == request.DepartmentId && s.EmployeeRole == CanonicalRole(request.EmployeeRole) && s.EffectiveTo == null).OrderByDescending(s => s.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (existing is not null && request.EffectiveFrom <= existing.EffectiveFrom) return Conflict(new { message = "A revision must start after the active structure's effective date." });
        if (existing is not null) { existing.EffectiveTo = request.EffectiveFrom.AddDays(-1); existing.UpdatedAtUtc = DateTime.UtcNow; }
        var structure = new SalaryStructure { DepartmentId = request.DepartmentId, EmployeeRole = CanonicalRole(request.EmployeeRole), MonthlyBasicSalary = request.MonthlyBasicSalary, OvertimeHourlyRate = request.OvertimeHourlyRate, Currency = request.Currency.Trim().ToUpperInvariant(), EffectiveFrom = request.EffectiveFrom, Revision = (existing?.Revision ?? 0) + 1, ApprovedByOwnerId = UserId()!.Value };
        _db.SalaryStructures.Add(structure); Audit("salary-structure.approved", structure.Id, $"Department {structure.DepartmentId}; role {structure.EmployeeRole}; revision {structure.Revision}");
        await _db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return StatusCode(201, structure);
    }

    [HttpGet("overrides")]
    public async Task<IActionResult> Overrides(CancellationToken ct) => !IsOwner() ? Forbid() : Ok(await _db.EmployeeSalaryOverrides.AsNoTracking().OrderByDescending(x => x.EffectiveFrom).ToListAsync(ct));

    [HttpPost("overrides")]
    public async Task<IActionResult> SaveOverride(SalaryOverrideRequest request, CancellationToken ct)
    {
        if (!IsOwner()) return Forbid(); if (request.EmployeeId == Guid.Empty || request.MonthlyBasicSalary <= 0 || request.OvertimeHourlyRate < 0 || !ValidCurrency(request.Currency) || string.IsNullOrWhiteSpace(request.Reason)) return BadRequest();
        var existing = await _db.EmployeeSalaryOverrides.Where(x => x.EmployeeId == request.EmployeeId && x.EffectiveTo == null).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (existing is not null && request.EffectiveFrom <= existing.EffectiveFrom) return Conflict(); if (existing is not null) existing.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        var item = new EmployeeSalaryOverride { EmployeeId = request.EmployeeId, MonthlyBasicSalary = request.MonthlyBasicSalary, OvertimeHourlyRate = request.OvertimeHourlyRate, Currency = request.Currency.Trim().ToUpperInvariant(), EffectiveFrom = request.EffectiveFrom, Reason = request.Reason.Trim(), ApprovedByOwnerId = UserId()!.Value };
        _db.EmployeeSalaryOverrides.Add(item); Audit("salary-override.approved", item.Id, $"Employee {item.EmployeeId}; effective {item.EffectiveFrom}"); await _db.SaveChangesAsync(ct); return StatusCode(201, item);
    }

    [HttpGet("allowances")]
    public async Task<IActionResult> Allowances(CancellationToken ct)
    {
        if (!IsOwner()) return Forbid(); return Ok(await _db.SalaryAllowanceConfigurations.AsNoTracking().OrderBy(a => a.DepartmentId).ThenBy(a => a.EmployeeRole).ThenBy(a => a.Kind).ThenByDescending(a => a.EffectiveFrom).ToListAsync(ct));
    }

    [HttpPost("allowances")]
    public async Task<IActionResult> SaveAllowance(AllowanceConfigurationRequest request, CancellationToken ct)
    {
        if (!IsOwner()) return Forbid();
        if (!request.DepartmentId.HasValue && !request.EmployeeId.HasValue || request.Value < 0 || request.CalculationType == AllowanceCalculationType.PercentageOfBasic && request.Value > 100 || !ValidCurrency(request.Currency)) return BadRequest(new { message = "Allowance target, valid value and currency are required." });
        if (request.EmployeeId is null && !ValidRole(request.EmployeeRole ?? "")) return BadRequest(new { message = "Role is required for department allowances." });
        var role = request.EmployeeRole is null ? null : CanonicalRole(request.EmployeeRole);
        var active = await _db.SalaryAllowanceConfigurations.FirstOrDefaultAsync(a => a.DepartmentId == request.DepartmentId && a.EmployeeRole == role && a.EmployeeId == request.EmployeeId && a.Kind == request.Kind && a.EffectiveTo == null, ct);
        if (active is not null && request.EffectiveFrom <= active.EffectiveFrom) return Conflict(new { message = "A later effective date is required for allowance revision." }); if (active is not null) active.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        var item = new SalaryAllowanceConfiguration { DepartmentId = request.DepartmentId, EmployeeRole = role, EmployeeId = request.EmployeeId, Kind = request.Kind, CalculationType = request.CalculationType, Value = request.Value, Currency = request.Currency.Trim().ToUpperInvariant(), EffectiveFrom = request.EffectiveFrom, ApprovedByOwnerId = UserId()!.Value };
        _db.SalaryAllowanceConfigurations.Add(item); Audit("salary-allowance.approved", item.Id, $"Kind {item.Kind}; target employee {item.EmployeeId}; department {item.DepartmentId}"); await _db.SaveChangesAsync(ct); return StatusCode(201, item);
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(CalculatePayrollRequest request, CancellationToken ct)
    {
        if (!await HasGrant(FinancePermissions.SubmitPayroll, ct)) return Forbid();
        if (!request.AttendanceVerified || request.Attendance is null || request.PeriodEnd < request.PeriodStart || request.ApprovedOvertimeHours < 0 || request.AuthorizedDeductions < 0 || !ValidRole(request.EmployeeRole)) return BadRequest(new { message = "A Department Manager-verified attendance and leave snapshot is required." });
        var attendance = request.Attendance;
        if (attendance.VerifiedByManagerId == Guid.Empty || attendance.VerifiedAtUtc == default || attendance.ScheduledDays < 0 || attendance.WorkedDays < 0 || attendance.WeekendDays < 0 || attendance.HolidayDays < 0 || attendance.ApprovedPaidLeaveDays < 0 || attendance.ApprovedUnpaidLeaveDays < 0) return BadRequest(new { message = "Attendance snapshot values and manager verification are invalid." });
        if (attendance.MissingPunches > 0 || attendance.DisputedRecords > 0) return Conflict(new { message = "Attendance discrepancies must be returned to the Department Manager and resolved before payroll finalization.", attendance.MissingPunches, attendance.DisputedRecords, attendance.VerifiedByManagerId });
        if (attendance.ApprovedOvertimeHours != request.ApprovedOvertimeHours) return BadRequest(new { message = "Overtime hours must match the manager-approved attendance snapshot." });
        if (attendance.SummaryId != request.SourcePayrollId || attendance.SummaryVersion < 1 || !await _attendanceVerifier.IsCurrentVerifiedAsync(attendance.SummaryId, request.EmployeeId, request.DepartmentId, request.PeriodEnd.Year, request.PeriodEnd.Month, attendance.SummaryVersion, request.ApprovedOvertimeHours, Request.Headers.Authorization, ct)) return Conflict(new { message = "Attendance summary is missing, unverified, outdated, or does not match this employee and payroll period." });
        var deductions = request.DeductionItems ?? Array.Empty<PayrollDeductionInput>();
        if (deductions.Any(d => d.Amount < 0 || string.IsNullOrWhiteSpace(d.Label) || string.IsNullOrWhiteSpace(d.RuleReference) || string.IsNullOrWhiteSpace(d.ValidationReference)) || deductions.Sum(d => d.Amount) != request.AuthorizedDeductions) return BadRequest(new { message = "Every deduction must be itemized and linked to a configured rule and validation reference." });
        var duplicate = await _db.FinancePayrollRecords.AsNoTracking().FirstOrDefaultAsync(p => p.SourcePayrollId == request.SourcePayrollId, ct); if (duplicate is not null) return Ok(duplicate);
        var effective = request.PeriodEnd; var role = CanonicalRole(request.EmployeeRole);
        var structure = await _db.SalaryStructures.AsNoTracking().Where(s => s.DepartmentId == request.DepartmentId && s.EmployeeRole == role && s.IsApproved && s.EffectiveFrom <= effective && (s.EffectiveTo == null || s.EffectiveTo >= effective)).OrderByDescending(s => s.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (structure is null) return Conflict(new { message = "Owner-approved salary structure is not configured for this department and role." });
        var employeeOverride = await _db.EmployeeSalaryOverrides.AsNoTracking().Where(x => x.EmployeeId == request.EmployeeId && x.EffectiveFrom <= effective && (x.EffectiveTo == null || x.EffectiveTo >= effective)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        var basic = employeeOverride?.MonthlyBasicSalary ?? structure.MonthlyBasicSalary; var overtimeRate = employeeOverride?.OvertimeHourlyRate ?? structure.OvertimeHourlyRate;
        var configurations = await _db.SalaryAllowanceConfigurations.AsNoTracking().Where(a => a.EffectiveFrom <= effective && (a.EffectiveTo == null || a.EffectiveTo >= effective) && (a.EmployeeId == request.EmployeeId || a.EmployeeId == null && a.DepartmentId == request.DepartmentId && a.EmployeeRole == role)).ToListAsync(ct);
        var applied = configurations.GroupBy(a => a.Kind).Select(g => g.OrderByDescending(x => x.EmployeeId == request.EmployeeId).ThenByDescending(x => x.EffectiveFrom).First()).ToList();
        var allowanceTotal = applied.Sum(a => a.CalculationType == AllowanceCalculationType.FixedAmount ? a.Value : decimal.Round(basic * a.Value / 100m, 2, MidpointRounding.AwayFromZero));
        var overtimePay = decimal.Round(request.ApprovedOvertimeHours * overtimeRate, 2, MidpointRounding.AwayFromZero); var gross = basic + allowanceTotal + overtimePay; var net = gross - request.AuthorizedDeductions; if (net < 0) return BadRequest(new { message = "Deductions cannot exceed gross salary." });
        var attendanceSnapshot = JsonSerializer.Serialize(attendance);
        var snapshot = JsonSerializer.Serialize(new { structure.Id, structure.Revision, BasicSalary = basic, OvertimeRate = overtimeRate, request.ApprovedOvertimeHours, OvertimePay = overtimePay, Allowances = applied.Select(a => new { a.Id, a.Kind, a.CalculationType, a.Value, Amount = a.CalculationType == AllowanceCalculationType.FixedAmount ? a.Value : decimal.Round(basic * a.Value / 100m, 2, MidpointRounding.AwayFromZero) }), AllowanceTotal = allowanceTotal, Deductions = deductions, DeductionTotal = request.AuthorizedDeductions, GrossSalary = gross, NetSalary = net, Attendance = attendance });
        var payroll = new FinancePayrollRecord { SourcePayrollId = request.SourcePayrollId, EmployeeId = request.EmployeeId, DepartmentId = request.DepartmentId, EmployeeRole = role, PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd, BaseSalary = basic, Allowances = allowanceTotal, OvertimeHours = request.ApprovedOvertimeHours, OvertimePay = overtimePay, GrossSalary = gross, Deductions = request.AuthorizedDeductions, NetSalary = net, Currency = employeeOverride?.Currency ?? structure.Currency, SalaryStructureId = structure.Id, CalculationSnapshotJson = snapshot, AttendanceSnapshotJson = attendanceSnapshot, SubmittedByUserId = UserId()!.Value, Status = PayrollApprovalStatus.PendingVerification };
        _db.FinancePayrollRecords.Add(payroll); Audit("payroll.calculated", payroll.Id, $"Employee {payroll.EmployeeId}; period {payroll.PeriodStart}-{payroll.PeriodEnd}; snapshot structure {structure.Id}"); await _db.SaveChangesAsync(ct); return StatusCode(201, payroll);
    }

    [HttpPost("payroll/{id:guid}/mock-payment")]
    public async Task<IActionResult> ProcessMockPayment(Guid id, MockPayrollPaymentRequest request, CancellationToken ct)
    {
        if (!await HasGrant(FinancePermissions.ExecutePayments, ct)) return Forbid(); if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) return BadRequest();
        var payroll = await _db.FinancePayrollRecords.FirstOrDefaultAsync(p => p.Id == id, ct); if (payroll is null) return NotFound();
        var payment = await _db.MockPayrollPayments.FirstOrDefaultAsync(p => p.PayrollId == id, ct);
        if (payment is not null && payment.IdempotencyKey == request.IdempotencyKey) return Ok(payment);
        if (payroll.Status != PayrollApprovalStatus.Approved && payroll.Status != PayrollApprovalStatus.Failed) return Conflict(new { message = "Mandatory Owner approval is required before simulated payment." });
        if (payment?.Status == MockPayrollPaymentStatus.MockPaid) return Conflict(new { message = "This payroll was already paid by the mock provider." });
        payment ??= new MockPayrollPayment { PayrollId = id, Payroll = payroll, InstructionReference = $"MOCK-SAL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", Amount = payroll.NetSalary, Currency = payroll.Currency, ProcessedByUserId = UserId()!.Value };
        payment.IdempotencyKey = request.IdempotencyKey.Trim(); payment.Scenario = request.Scenario; payment.AttemptCount++; payment.Status = MockPayrollPaymentStatus.Processing; payment.FailureReason = null; payment.UpdatedAtUtc = DateTime.UtcNow; payroll.Status = PayrollApprovalStatus.DisbursementPending;
        switch (request.Scenario)
        {
            case MockPaymentScenario.Success: payment.Status = MockPayrollPaymentStatus.MockPaid; payment.MockProviderReference = $"MOCK-{Guid.NewGuid():N}"; payment.ConfirmedAtUtc = DateTime.UtcNow; payroll.Status = PayrollApprovalStatus.Disbursed; payroll.DisbursedAtUtc = payment.ConfirmedAtUtc; payroll.DisbursementReference = payment.MockProviderReference; break;
            case MockPaymentScenario.Failure: payment.Status = MockPayrollPaymentStatus.PaymentFailed; payment.FailureReason = "Simulated provider rejection"; payroll.Status = PayrollApprovalStatus.Failed; break;
            case MockPaymentScenario.Timeout: payment.Status = MockPayrollPaymentStatus.TimedOut; payment.FailureReason = "Simulated provider timeout; safe retry allowed"; payroll.Status = PayrollApprovalStatus.Failed; break;
            case MockPaymentScenario.DelayedSuccess: payment.Status = MockPayrollPaymentStatus.Delayed; payment.MockProviderReference = $"MOCK-DELAYED-{Guid.NewGuid():N}"; break;
        }
        if (_db.Entry(payment).State == EntityState.Detached) _db.MockPayrollPayments.Add(payment); Audit("mock-payroll-payment.processed", payment.Id, $"SIMULATED ONLY; payroll {id}; status {payment.Status}; attempt {payment.AttemptCount}"); await _db.SaveChangesAsync(ct); return Ok(payment);
    }

    [HttpPost("mock-payments/{id:guid}/confirm-delayed")]
    public async Task<IActionResult> ConfirmDelayed(Guid id, CancellationToken ct)
    {
        if (!await HasGrant(FinancePermissions.ExecutePayments, ct)) return Forbid(); var payment = await _db.MockPayrollPayments.Include(p => p.Payroll).FirstOrDefaultAsync(p => p.Id == id, ct); if (payment?.Payroll is null) return NotFound(); if (payment.Status != MockPayrollPaymentStatus.Delayed) return Conflict();
        payment.Status = MockPayrollPaymentStatus.MockPaid; payment.ConfirmedAtUtc = DateTime.UtcNow; payment.Payroll.Status = PayrollApprovalStatus.Disbursed; payment.Payroll.DisbursedAtUtc = payment.ConfirmedAtUtc; payment.Payroll.DisbursementReference = payment.MockProviderReference; Audit("mock-payroll-payment.confirmed", payment.Id, "SIMULATED delayed confirmation"); await _db.SaveChangesAsync(ct); return Ok(payment);
    }

    [HttpGet("mock-payments")]
    public async Task<IActionResult> MockPayments(CancellationToken ct) => IsOwner() || await HasGrant(FinancePermissions.ViewPayroll, ct) ? Ok(await _db.MockPayrollPayments.AsNoTracking().OrderByDescending(p => p.CreatedAtUtc).Take(500).ToListAsync(ct)) : Forbid();

    [HttpGet("me")]
    public async Task<IActionResult> MySalary(CancellationToken ct)
    {
        var employee = UserId(); if (!employee.HasValue) return Unauthorized();
        var payrolls = await _db.FinancePayrollRecords.AsNoTracking().Where(p => p.EmployeeId == employee).OrderByDescending(p => p.PeriodEnd).ToListAsync(ct);
        var latest = payrolls.FirstOrDefault(); var paymentIds = payrolls.Select(p => p.Id).ToList(); var payments = await _db.MockPayrollPayments.AsNoTracking().Where(p => paymentIds.Contains(p.PayrollId)).ToListAsync(ct);
        if (latest is null) return Ok(new { current = (object?)null, payrolls, payments });
        var effective = DateOnly.FromDateTime(DateTime.UtcNow);
        var allowances = await _db.SalaryAllowanceConfigurations.AsNoTracking().Where(a => a.EffectiveFrom <= effective && (a.EffectiveTo == null || a.EffectiveTo >= effective) && (a.EmployeeId == employee || a.EmployeeId == null && a.DepartmentId == latest.DepartmentId && a.EmployeeRole == latest.EmployeeRole)).ToListAsync(ct);
        return Ok(new { current = new { latest.BaseSalary, latest.Currency, latest.DepartmentId, latest.EmployeeRole }, allowances, payrolls, payments });
    }

    private Task<bool> HasGrant(string permission, CancellationToken ct) => UserId() is { } id && IsFinance() ? _db.FinanceAccessGrants.AnyAsync(g => g.UserId == id && g.Permission == permission && g.IsActive, ct) : Task.FromResult(false);
    private void Audit(string action, Guid id, string details) => _db.FinanceAuditLogs.Add(new FinanceAuditLog { ActorUserId = UserId()!.Value, Action = action, EntityType = "SalaryPayroll", EntityId = id, Details = details });
    private Guid? UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
    private string Role() => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "";
    private bool IsOwner() => Role() == "Owner";
    private bool IsFinance() => string.Equals(User.FindFirstValue("departmentCode"), "FINANCE", StringComparison.OrdinalIgnoreCase);
    private static bool ValidCurrency(string? value) => value?.Trim().Length == 3 && value.Trim().All(char.IsLetter);
    private static bool ValidRole(string role) => role.Trim().Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Trim().Equals("Manager", StringComparison.OrdinalIgnoreCase) || role.Trim().Equals("Employee", StringComparison.OrdinalIgnoreCase);
    private static string CanonicalRole(string role) => char.ToUpperInvariant(role.Trim()[0]) + role.Trim()[1..].ToLowerInvariant();
}
