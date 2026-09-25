using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;

namespace SmartHotel.Booking.API.Controllers;

public record CreateExpenseRequest(decimal Amount, string Currency, string Description, string Category, string? VendorReference, string IdempotencyKey, Guid DepartmentId, string? ReceiptUrl = null);
public record CreateRefundRequest(Guid PaymentId, decimal Amount, string Currency, string Description, string IdempotencyKey, Guid DepartmentId);
public record FinanceDecisionRequest(bool Approve, string? Reason);
public record FinanceSettingsRequest(decimal ManagerExpenseApprovalLimit, decimal ManagerRefundApprovalLimit, string Currency, decimal ManagerReleaseApprovalLimit = 0);
public record FinanceGrantRequest(Guid UserId, string Permission, bool IsActive = true);

[ApiController]
[Route("api/v1/finance")]
[Authorize]
public class FinanceController : ControllerBase
{
    private readonly BookingDbContext _db;
    private static readonly HashSet<string> KnownPermissions =
    [
        FinancePermissions.ViewReports, FinancePermissions.ApproveExpenses, FinancePermissions.ApproveRefunds,
        FinancePermissions.SubmitPayroll, FinancePermissions.ExecutePayments,
        FinancePermissions.ApproveReleases, FinancePermissions.ManageConfiguration, FinancePermissions.ViewAudit,
        FinancePermissions.ManageInvoices, FinancePermissions.VerifyExpenses, FinancePermissions.ReconcileSettlements,
        FinancePermissions.ViewPayroll, FinancePermissions.ApprovePayroll
    ];

    public FinanceController(BookingDbContext db) => _db = db;

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        if (!await CanViewReports(ct)) return Forbid();
        var completed = await _db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.FundsHeld || p.Status == PaymentStatus.ReleasePending || p.Status == PaymentStatus.Released || p.Status == PaymentStatus.Settled || p.Status == PaymentStatus.PartiallyRefunded).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var refundedPayments = await _db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Refunded).ToListAsync(ct);
        var refunded = await _db.Payments.AsNoTracking().SumAsync(p => (decimal?)p.RefundedAmount, ct) ?? 0m;
        var retainedFromRefunded = refundedPayments.Sum(p => p.Amount - (p.RefundAmount ?? 0m));
        var outstanding = await _db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Created).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var approvedExpenses = await _db.ExpenseRequests.AsNoTracking().Where(e => e.Status == FinanceRequestStatus.Approved).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        return Ok(new
        {
            currency = "LKR",
            collectedRevenue = completed + retainedFromRefunded,
            recordedExpenses = approvedExpenses,
            outstandingPayments = outstanding,
            refundedAmount = refunded,
            pendingExpenses = await _db.ExpenseRequests.CountAsync(e => e.Status == FinanceRequestStatus.Pending, ct),
            pendingRefunds = await _db.RefundRequests.CountAsync(e => e.Status == FinanceRequestStatus.Pending, ct),
            heldFunds = await _db.Payments.Where(p => p.Status == PaymentStatus.FundsHeld || p.Status == PaymentStatus.ReleasePending).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m,
            releasedFunds = await _db.Payments.Where(p => p.Status == PaymentStatus.Released).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m,
            settledFunds = await _db.SettlementRecords.Where(s => s.Status == SettlementStatus.Confirmed).SumAsync(s => (decimal?)s.Amount, ct) ?? 0m,
            metricDefinition = "Collected revenue is completed payment value less recorded refunds; it is not accounting profit."
        });
    }

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses(CancellationToken ct)
    {
        if (!await CanViewReports(ct)) return Forbid();
        return Ok(await _db.ExpenseRequests.AsNoTracking().OrderByDescending(e => e.CreatedAtUtc).Take(500).ToListAsync(ct));
    }

    [HttpPost("expenses")]
    public async Task<IActionResult> SubmitExpense(CreateExpenseRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (!userId.HasValue || DepartmentId() != request.DepartmentId || Role() is not ("Admin" or "Manager" or "Employee")) return Forbid();
        if (request.Amount <= 0 || !IsCurrency(request.Currency) || string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.IdempotencyKey)) return BadRequest(new { message = "Positive amount, ISO currency, description and idempotency key are required." });
        var existing = await _db.ExpenseRequests.AsNoTracking().FirstOrDefaultAsync(e => e.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null) return Ok(existing);
        var expense = new ExpenseRequest { Amount = request.Amount, Currency = Currency(request.Currency), Description = request.Description.Trim(), Category = request.Category.Trim(), VendorReference = request.VendorReference?.Trim(), ReceiptUrl = request.ReceiptUrl?.Trim(), IdempotencyKey = request.IdempotencyKey.Trim(), SubmittedByUserId = userId.Value, SubmittedByDepartmentId = request.DepartmentId };
        _db.ExpenseRequests.Add(expense);
        Audit(userId.Value, "expense.submitted", nameof(ExpenseRequest), expense.Id, $"Amount {expense.Amount} {expense.Currency}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new { message = "An expense with this idempotency key already exists." }); }
        return CreatedAtAction(nameof(Expenses), new { id = expense.Id }, expense);
    }

    [HttpPost("refunds")]
    public async Task<IActionResult> SubmitRefund(CreateRefundRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (!userId.HasValue || !CanSubmitFinanceRequest() || DepartmentId() != request.DepartmentId) return Forbid();
        if (!IsCurrency(request.Currency) || string.IsNullOrWhiteSpace(request.IdempotencyKey)) return BadRequest(new { message = "ISO currency and idempotency key are required." });
        await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        using var paymentLock = await _db.AcquirePaymentLockAsync($"refund:{request.PaymentId}", ct);
        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct);
        var refundableStatuses = new[] { PaymentStatus.Completed, PaymentStatus.FundsHeld, PaymentStatus.ReleasePending, PaymentStatus.Released, PaymentStatus.Settled, PaymentStatus.PartiallyRefunded };
        var pendingOrExecuted = await _db.RefundRequests.Where(r => r.PaymentId == request.PaymentId && r.Status != FinanceRequestStatus.Rejected && r.Status != FinanceRequestStatus.Cancelled).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        if (payment is null || !refundableStatuses.Contains(payment.Status) || request.Amount <= 0 || request.Amount + pendingOrExecuted > payment.Amount) return BadRequest(new { message = "Refund must reference a verified payment and cannot exceed its remaining refundable amount." });
        var existing = await _db.RefundRequests.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null) return Ok(existing);
        var refund = new RefundRequest { PaymentId = request.PaymentId, Amount = request.Amount, Currency = Currency(request.Currency), Description = request.Description.Trim(), IdempotencyKey = request.IdempotencyKey.Trim(), SubmittedByUserId = userId.Value, SubmittedByDepartmentId = request.DepartmentId };
        _db.RefundRequests.Add(refund);
        Audit(userId.Value, "refund.submitted", nameof(RefundRequest), refund.Id, $"Payment {payment.Id}; amount {refund.Amount} {refund.Currency}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new { message = "A refund request already exists for this payment or idempotency key." }); }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return StatusCode(StatusCodes.Status201Created, refund);
    }

    [HttpGet("refunds")]
    public async Task<IActionResult> Refunds(CancellationToken ct)
    {
        if (!await CanViewReports(ct)) return Forbid();
        return Ok(await _db.RefundRequests.AsNoTracking().OrderByDescending(r => r.CreatedAtUtc).Take(500).ToListAsync(ct));
    }

    [HttpPost("expenses/{id:guid}/decision")]
    public Task<IActionResult> DecideExpense(Guid id, FinanceDecisionRequest request, CancellationToken ct) => Decide<ExpenseRequest>(id, request, FinancePermissions.ApproveExpenses, s => s.ManagerExpenseApprovalLimit, ct);

    [HttpPost("refunds/{id:guid}/decision")]
    public Task<IActionResult> DecideRefund(Guid id, FinanceDecisionRequest request, CancellationToken ct) => Decide<RefundRequest>(id, request, FinancePermissions.ApproveRefunds, s => s.ManagerRefundApprovalLimit, ct);

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        if (!IsOwner() && !IsFinanceDepartment()) return Forbid();
        return Ok(await _db.FinanceApprovalSettings.AsNoTracking().OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc).FirstOrDefaultAsync(ct));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(FinanceSettingsRequest request, CancellationToken ct)
    {
        if (!IsOwner() && !await HasGrant(FinancePermissions.ManageConfiguration, ct)) return Forbid();
        if (request.ManagerExpenseApprovalLimit < 0 || request.ManagerRefundApprovalLimit < 0 || request.ManagerReleaseApprovalLimit < 0 || !IsCurrency(request.Currency)) return BadRequest(new { message = "Approval limits and ISO currency are invalid." });
        var settings = await _db.FinanceApprovalSettings.FirstOrDefaultAsync(ct) ?? new FinanceApprovalSettings();
        settings.ManagerExpenseApprovalLimit = request.ManagerExpenseApprovalLimit; settings.ManagerRefundApprovalLimit = request.ManagerRefundApprovalLimit; settings.ManagerReleaseApprovalLimit = request.ManagerReleaseApprovalLimit; settings.Currency = Currency(request.Currency); settings.UpdatedByUserId = UserId()!.Value; settings.UpdatedAtUtc = DateTime.UtcNow;
        if (settings.Id == Guid.Empty) _db.FinanceApprovalSettings.Add(settings);
        Audit(UserId()!.Value, "settings.updated", nameof(FinanceApprovalSettings), settings.Id, "Approval thresholds updated");
        await _db.SaveChangesAsync(ct); return Ok(settings);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> AuditLog(CancellationToken ct)
    {
        if (!IsOwner() && !await HasGrant(FinancePermissions.ViewAudit, ct)) return Forbid();
        return Ok(await _db.FinanceAuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc).Take(500).ToListAsync(ct));
    }

    [HttpGet("access")]
    public async Task<IActionResult> Access(CancellationToken ct)
    {
        if (!IsOwner() && !(IsFinanceDepartment() && Role() == "Admin")) return Forbid();
        return Ok(await _db.FinanceAccessGrants.AsNoTracking().OrderBy(g => g.UserId).ToListAsync(ct));
    }

    [HttpPut("access")]
    public async Task<IActionResult> Grant(FinanceGrantRequest request, CancellationToken ct)
    {
        if (!KnownPermissions.Contains(request.Permission)) return BadRequest(new { message = "Unknown finance permission." });
        var safeAdminGrant = request.Permission is FinancePermissions.ViewReports or FinancePermissions.ViewAudit;
        if (!IsOwner() && !(IsFinanceDepartment() && Role() == "Admin" && safeAdminGrant)) return Forbid();
        var grant = await _db.FinanceAccessGrants.FirstOrDefaultAsync(g => g.UserId == request.UserId && g.Permission == request.Permission, ct);
        if (grant is null) { grant = new FinanceAccessGrant { UserId = request.UserId, Permission = request.Permission, GrantedByUserId = UserId()!.Value, IsActive = request.IsActive }; _db.Add(grant); }
        else { grant.IsActive = request.IsActive; grant.GrantedByUserId = UserId()!.Value; grant.UpdatedAtUtc = DateTime.UtcNow; }
        Audit(UserId()!.Value, "access.updated", nameof(FinanceAccessGrant), grant.Id, $"{grant.Permission}: {grant.IsActive}");
        await _db.SaveChangesAsync(ct); return Ok(grant);
    }

    private async Task<IActionResult> Decide<T>(Guid id, FinanceDecisionRequest request, string permission, Func<FinanceApprovalSettings, decimal> limit, CancellationToken ct) where T : FinanceRequest
    {
        var actor = UserId(); if (!actor.HasValue) return Unauthorized();
        var entity = await _db.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct); if (entity is null) return NotFound();
        if (entity.SubmittedByUserId == actor.Value) return Forbid();
        var authorizedForStage = !request.Approve
            ? IsOwner() || await HasGrant(permission, ct)
            : entity.ApprovalStage == FinanceApprovalStage.Manager
                ? IsFinanceDepartment() && Role() == "Manager" && await HasGrant(permission, ct)
                : IsOwner();
        if (!authorizedForStage) return Forbid();
        if (entity.Status != FinanceRequestStatus.Pending) return Conflict(new { message = "This request already has a final decision." });
        if (entity is ExpenseRequest expense && expense.VerificationStatus != ExpenseVerificationStatus.Verified) return Conflict(new { message = "Finance document verification is required before approval." });
        var settings = await _db.FinanceApprovalSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null) return Conflict(new { message = "Finance approval thresholds must be configured by the Owner before decisions are allowed." });
        if (!request.Approve) { entity.Status = FinanceRequestStatus.Rejected; entity.ApprovalStage = FinanceApprovalStage.Complete; }
        else if (entity.ApprovalStage == FinanceApprovalStage.Manager)
        {
            if (!string.Equals(entity.Currency, settings.Currency, StringComparison.OrdinalIgnoreCase)) return Conflict(new { message = "Request currency does not match configured approval currency." });
            if (entity.Amount > limit(settings)) entity.ApprovalStage = FinanceApprovalStage.Owner;
            else { entity.Status = FinanceRequestStatus.Approved; entity.ApprovalStage = FinanceApprovalStage.Complete; }
        }
        else { entity.Status = FinanceRequestStatus.Approved; entity.ApprovalStage = FinanceApprovalStage.Complete; }
        entity.DecidedByUserId = actor; entity.DecidedAtUtc = DateTime.UtcNow; entity.DecisionReason = request.Reason?.Trim(); entity.UpdatedAtUtc = DateTime.UtcNow;
        Audit(actor.Value, request.Approve ? "request.approved" : "request.rejected", typeof(T).Name, entity.Id, $"Stage {entity.ApprovalStage}");
        await _db.SaveChangesAsync(ct); return Ok(entity);
    }

    private async Task<bool> CanViewReports(CancellationToken ct) => IsOwner() || IsFinanceDepartment() && (Role() == "Manager" || await HasGrant(FinancePermissions.ViewReports, ct));
    private Task<bool> HasGrant(string permission, CancellationToken ct) { var id = UserId(); return id.HasValue && IsFinanceDepartment() ? _db.FinanceAccessGrants.AnyAsync(g => g.UserId == id && g.Permission == permission && g.IsActive, ct) : Task.FromResult(false); }
    private void Audit(Guid actor, string action, string type, Guid id, string details) => _db.FinanceAuditLogs.Add(new FinanceAuditLog { ActorUserId = actor, Action = action, EntityType = type, EntityId = id, Details = details });
    private Guid? UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
    private Guid? DepartmentId() => Guid.TryParse(User.FindFirstValue("departmentId"), out var id) ? id : null;
    private string Role() => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "";
    private bool IsOwner() => Role() == "Owner";
    private bool IsFinanceDepartment() => string.Equals(User.FindFirstValue("departmentCode"), "FINANCE", StringComparison.OrdinalIgnoreCase);
    private bool CanSubmitFinanceRequest() => IsFinanceDepartment() && (Role() == "Manager" || User.FindFirstValue("designation") is "Accountant" or "Finance Assistant");
    private static bool IsCurrency(string? value) => value?.Trim().Length == 3 && value.Trim().All(char.IsLetter);
    private static string Currency(string value) => string.IsNullOrWhiteSpace(value) ? "LKR" : value.Trim().ToUpperInvariant() is var c && c.Length == 3 ? c : throw new ArgumentException("Currency must be a 3-letter ISO code.");
}
