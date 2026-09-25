using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;

namespace SmartHotel.Booking.API.Controllers;

public record PrepareReleaseRequest(Guid PaymentId, string IdempotencyKey, Guid DepartmentId, string? Notes);
public record EscrowDecisionRequest(bool Approve, string? Reason);
public record ExecuteEscrowRequest(string IdempotencyKey);
public record CreateEscrowCheckoutRequest(Guid BookingId, string IdempotencyKey);

[ApiController]
[Route("api/v1/escrow")]
[Authorize]
public sealed class EscrowController : ControllerBase
{
    private readonly BookingDbContext _db;
    private readonly IEscrowPaymentProvider _provider;
    public EscrowController(BookingDbContext db, IEscrowPaymentProvider provider) { _db = db; _provider = provider; }

    [HttpGet("capabilities")]
    [AllowAnonymous]
    public IActionResult Capabilities() => Ok(new { provider = _provider.Provider.ToString(), _provider.Capabilities, configured = _provider.Capabilities.CanHoldFunds });

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout(CreateEscrowCheckoutRequest request, CancellationToken ct)
    {
        if (!_provider.Capabilities.CanHoldFunds) return StatusCode(501, new { message = "No escrow-capable checkout provider is configured." });
        var booking = await _db.Bookings.Include(b => b.Payments).FirstOrDefaultAsync(b => b.Id == request.BookingId, ct);
        if (booking is null) return NotFound();
        if (booking.Status != BookingStatus.PendingPayment || (UserId().HasValue && !IsOwner() && booking.CustomerId != UserId())) return Forbid();
        var existingLifecycle = await _db.EscrowLifecycleRecords.AsNoTracking().FirstOrDefaultAsync(e => e.IdempotencyKey == request.IdempotencyKey, ct);
        if (existingLifecycle is not null) return Ok(new { paymentId = existingLifecycle.PaymentId, existingLifecycle.ProviderReference, duplicate = true });
        if (string.IsNullOrWhiteSpace(booking.Currency)) return StatusCode(422, new { message = "Booking currency is missing." });
        var currency = booking.Currency.ToUpperInvariant();
        var result = await _provider.CreateCheckoutAsync(booking.BookingReference, booking.TotalAmount, currency, request.IdempotencyKey, ct);
        if (!result.Succeeded) return StatusCode(502, new { message = result.Error });
        var payment = new Payment { Id = Guid.NewGuid(), BookingId = booking.Id, Amount = booking.TotalAmount, Currency = currency, Provider = _provider.Provider, PayHereOrderId = booking.BookingReference, ProviderCheckoutReference = result.CheckoutReference, ProviderPaymentReference = result.PaymentReference, Status = PaymentStatus.Created };
        _db.Payments.Add(payment);
        AddLifecycle(payment.Id, EscrowEventType.CheckoutCreated, result.CheckoutReference, request.IdempotencyKey, "Escrow checkout created; awaiting signed funds-held webhook");
        try { await _db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new { message = "Checkout creation was duplicated." }); }
        return Ok(new { payment.Id, result.CheckoutUrl, result.CheckoutReference });
    }

    [HttpGet("payments/{paymentId:guid}")]
    public async Task<IActionResult> Status(Guid paymentId, CancellationToken ct)
    {
        var payment = await _db.Payments.AsNoTracking().Include(p => p.Booking).FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return NotFound();
        if (!CanSeePayment(payment)) return Forbid();
        var lifecycle = await _db.EscrowLifecycleRecords.AsNoTracking().Where(e => e.PaymentId == paymentId).OrderBy(e => e.CreatedAtUtc).ToListAsync(ct);
        var settlements = await _db.SettlementRecords.AsNoTracking().Where(e => e.PaymentId == paymentId).OrderBy(e => e.CreatedAtUtc).ToListAsync(ct);
        return Ok(new { payment.Id, payment.BookingId, payment.Amount, payment.Currency, payment.Status, payment.RefundedAmount, lifecycle, settlements });
    }

    [HttpPost("releases")]
    public async Task<IActionResult> PrepareRelease(PrepareReleaseRequest request, CancellationToken ct)
    {
        var actor = UserId();
        if (!actor.HasValue || !IsFinance() || DepartmentId() != request.DepartmentId || Role() is not ("Employee" or "Manager")) return Forbid();
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) return BadRequest(new { message = "Idempotency key is required." });
        var existing = await _db.EscrowReleaseRequests.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null) return Ok(existing);
        var payment = await _db.Payments.Include(p => p.Booking).FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct);
        if (payment?.Booking?.Status != BookingStatus.CheckedOut || payment.Status != PaymentStatus.FundsHeld)
            return Conflict(new { message = "Verified held funds may be prepared only after guest checkout." });
        var release = new EscrowReleaseRequest { PaymentId = payment.Id, Amount = payment.Amount - payment.RefundedAmount, Currency = payment.Currency, Description = request.Notes?.Trim() ?? "Post-checkout escrow release", IdempotencyKey = request.IdempotencyKey.Trim(), SubmittedByUserId = actor.Value, SubmittedByDepartmentId = request.DepartmentId };
        _db.EscrowReleaseRequests.Add(release);
        AddHistory(release.Id, actor.Value, FinanceApprovalStage.Manager, EscrowApprovalAction.Submitted, request.Notes);
        try { await _db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new { message = "A release request already exists for this payment or idempotency key." }); }
        return StatusCode(StatusCodes.Status201Created, release);
    }

    [HttpGet("releases")]
    public async Task<IActionResult> Releases(CancellationToken ct)
    {
        if (!IsOwner() && !IsFinance()) return Forbid();
        return Ok(await _db.EscrowReleaseRequests.AsNoTracking().OrderByDescending(r => r.CreatedAtUtc).Take(500).ToListAsync(ct));
    }

    [HttpPost("releases/{id:guid}/decision")]
    public async Task<IActionResult> DecideRelease(Guid id, EscrowDecisionRequest request, CancellationToken ct)
    {
        var actor = UserId(); if (!actor.HasValue) return Unauthorized();
        var release = await _db.EscrowReleaseRequests.FirstOrDefaultAsync(r => r.Id == id, ct); if (release is null) return NotFound();
        if (release.Status != FinanceRequestStatus.Pending || release.SubmittedByUserId == actor) return Conflict(new { message = "Release is final or self-approval was attempted." });
        var settings = await _db.FinanceApprovalSettings.AsNoTracking().FirstOrDefaultAsync(ct); if (settings is null) return Conflict(new { message = "Finance thresholds are not configured." });
        if (!request.Approve)
        {
            if (!IsOwner() && !await HasGrant(FinancePermissions.ApproveReleases, ct)) return Forbid();
            release.Status = FinanceRequestStatus.Rejected; release.ApprovalStage = FinanceApprovalStage.Complete;
        }
        else if (release.ApprovalStage == FinanceApprovalStage.Manager)
        {
            if (!IsFinance() || Role() != "Manager" || !await HasGrant(FinancePermissions.ApproveReleases, ct)) return Forbid();
            if (release.Amount > settings.ManagerReleaseApprovalLimit) release.ApprovalStage = FinanceApprovalStage.Owner;
            else { release.Status = FinanceRequestStatus.Approved; release.ApprovalStage = FinanceApprovalStage.Complete; }
        }
        else
        {
            if (!IsOwner()) return Forbid(); release.Status = FinanceRequestStatus.Approved; release.ApprovalStage = FinanceApprovalStage.Complete;
        }
        release.DecidedByUserId = actor; release.DecidedAtUtc = DateTime.UtcNow; release.DecisionReason = request.Reason?.Trim(); release.UpdatedAtUtc = DateTime.UtcNow;
        AddHistory(release.Id, actor.Value, release.ApprovalStage, request.Approve ? EscrowApprovalAction.Approved : EscrowApprovalAction.Rejected, request.Reason);
        await _db.SaveChangesAsync(ct); return Ok(release);
    }

    [HttpPost("releases/{id:guid}/execute")]
    public async Task<IActionResult> ExecuteRelease(Guid id, ExecuteEscrowRequest request, CancellationToken ct)
    {
        if (!IsFinance() || !await HasGrant(FinancePermissions.ExecutePayments, ct)) return Forbid();
        if (!_provider.Capabilities.CanReleaseFunds) return StatusCode(501, new { message = "No escrow-capable release provider is configured; no money movement was attempted." });
        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        if (_db.Database.ProviderName?.Contains("Npgsql") == true) await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({$"escrow-release:{id}"}));", ct);
        var release = await _db.EscrowReleaseRequests.Include(r => r.Payment).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (release?.Payment is null) return NotFound();
        if (release.Status != FinanceRequestStatus.Approved || release.ExecutionStatus == FinanceExecutionStatus.Executed) return Conflict(new { message = "Release is not approved or was already executed." });
        if (release.Payment.Status != PaymentStatus.FundsHeld) return Conflict(new { message = "Payment funds are not in a verified held state." });
        release.ExecutionStatus = FinanceExecutionStatus.Processing; release.Payment.Status = PaymentStatus.ReleasePending; await _db.SaveChangesAsync(ct);
        var result = await _provider.ReleaseAsync(release.Payment.ProviderPaymentReference ?? "", release.Amount, release.Currency, request.IdempotencyKey, ct);
        if (!result.Succeeded || !result.Verified) { release.ExecutionStatus = FinanceExecutionStatus.Failed; release.Payment.Status = PaymentStatus.FundsHeld; await _db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return StatusCode(502, new { message = result.Error ?? "Provider release was not verified." }); }
        release.ExecutionStatus = FinanceExecutionStatus.Executed; release.ProviderReleaseReference = result.ProviderReference;
        release.Payment.Status = PaymentStatus.Released; release.Payment.ReleasedAtUtc = DateTime.UtcNow;
        AddLifecycle(release.PaymentId, EscrowEventType.Released, result.ProviderReference, request.IdempotencyKey, "Verified provider release");
        AddHistory(release.Id, UserId()!.Value, FinanceApprovalStage.Complete, EscrowApprovalAction.Executed, null);
        await _db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return Ok(release);
    }

    [HttpPost("refunds/{id:guid}/execute")]
    public async Task<IActionResult> ExecuteRefund(Guid id, ExecuteEscrowRequest request, CancellationToken ct)
    {
        if (!IsFinance() || !await HasGrant(FinancePermissions.ExecutePayments, ct)) return Forbid();
        if (!_provider.Capabilities.CanRefund) return StatusCode(501, new { message = "No escrow-capable refund provider is configured; refund remains approved but unexecuted." });
        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        if (_db.Database.ProviderName?.Contains("Npgsql") == true) await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({$"escrow-refund:{id}"}));", ct);
        var refund = await _db.RefundRequests.Include(r => r.Payment).FirstOrDefaultAsync(r => r.Id == id, ct); if (refund?.Payment is null) return NotFound();
        if (refund.Status != FinanceRequestStatus.Approved || refund.ExecutionStatus == FinanceExecutionStatus.Executed) return Conflict(new { message = "Refund is not approved or was already executed." });
        var totalExecuted = await _db.RefundRequests.Where(r => r.PaymentId == refund.PaymentId && r.ExecutionStatus == FinanceExecutionStatus.Executed).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        if (totalExecuted + refund.Amount > refund.Payment.Amount) return Conflict(new { message = "Refund exceeds remaining refundable amount." });
        refund.ExecutionStatus = FinanceExecutionStatus.Processing; refund.Payment.Status = PaymentStatus.RefundPending; await _db.SaveChangesAsync(ct);
        var result = await _provider.RefundAsync(refund.Payment.ProviderPaymentReference ?? "", refund.Amount, refund.Currency, request.IdempotencyKey, ct);
        if (!result.Succeeded || !result.Verified) { refund.ExecutionStatus = FinanceExecutionStatus.Failed; await _db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return StatusCode(502, new { message = result.Error ?? "Provider refund was not verified." }); }
        refund.ExecutionStatus = FinanceExecutionStatus.Executed; refund.Payment.RefundedAmount = totalExecuted + refund.Amount; refund.Payment.RefundAmount = refund.Payment.RefundedAmount; refund.Payment.RefundedAt = DateTime.UtcNow;
        refund.Payment.Status = refund.Payment.RefundedAmount == refund.Payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        AddLifecycle(refund.PaymentId, EscrowEventType.Refunded, result.ProviderReference, request.IdempotencyKey, $"Verified refund {refund.Amount} {refund.Currency}");
        await _db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return Ok(refund);
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromHeader(Name = "X-Escrow-Signature")] string signature, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8); var raw = await reader.ReadToEndAsync(ct);
        if (!_provider.TryVerifyWebhook(raw, signature ?? "", out var evt) || evt is null) return Unauthorized(new { message = "Invalid webhook signature or escrow provider is not configured." });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        if (await _db.PaymentWebhookReceipts.AnyAsync(r => r.Provider == _provider.Provider && r.ProviderEventId == evt.EventId, ct)) return Ok(new { duplicate = true });
        var payment = await _db.Payments.Include(p => p.Booking).FirstOrDefaultAsync(p => p.ProviderPaymentReference == evt.PaymentReference, ct); if (payment is null) return NotFound();
        var isSettlement = evt.EventType.Equals("settled", StringComparison.OrdinalIgnoreCase);
        if ((!isSettlement && evt.Amount != payment.Amount) || (isSettlement && (evt.Amount <= 0 || evt.Amount > payment.Amount)) || !string.Equals(evt.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Webhook amount or currency does not match the payment." });
        switch (evt.EventType.ToLowerInvariant())
        {
            case "funds_held" when _provider.Capabilities.CanHoldFunds:
                payment.Status = PaymentStatus.FundsHeld; payment.FundsHeldAtUtc = DateTime.UtcNow;
                if (payment.Booking?.Status == BookingStatus.PendingPayment) payment.Booking.ConfirmPayment(evt.PaymentReference, payment.ProviderCheckoutReference ?? evt.PaymentReference);
                AddLifecycle(payment.Id, EscrowEventType.FundsHeld, evt.OperationReference, $"webhook:{evt.EventId}", "Signed provider hold confirmation"); break;
            case "settled" when _provider.Capabilities.CanReportSettlement:
                payment.Status = PaymentStatus.Settled; payment.SettledAtUtc = DateTime.UtcNow;
                _db.SettlementRecords.Add(new SettlementRecord { PaymentId = payment.Id, Amount = evt.Amount, Currency = evt.Currency.ToUpperInvariant(), Status = SettlementStatus.Confirmed, ProviderSettlementReference = evt.OperationReference, ConfirmedAtUtc = DateTime.UtcNow });
                AddLifecycle(payment.Id, EscrowEventType.SettlementConfirmed, evt.OperationReference, $"webhook:{evt.EventId}", "Signed settlement confirmation"); break;
            case "payment_failed":
                payment.Status = PaymentStatus.Failed;
                AddLifecycle(payment.Id, EscrowEventType.Failed, evt.OperationReference, $"webhook:{evt.EventId}", "Signed provider payment failure"); break;
            default: return BadRequest(new { message = "Unsupported or unavailable escrow event capability." });
        }
        _db.PaymentWebhookReceipts.Add(new PaymentWebhookReceipt { Provider = _provider.Provider, ProviderEventId = evt.EventId, EventType = evt.EventType, PayloadHash = hash, ProcessedAtUtc = DateTime.UtcNow });
        try { await _db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Ok(new { duplicate = true }); }
        return Ok(new { processed = true });
    }

    private bool CanSeePayment(Payment payment) => IsOwner() || IsFinance() || IsFrontOffice() || (UserId().HasValue && payment.Booking?.CustomerId == UserId());
    private void AddLifecycle(Guid payment, EscrowEventType type, string providerRef, string key, string details) => _db.EscrowLifecycleRecords.Add(new EscrowLifecycleRecord { PaymentId = payment, EventType = type, ProviderReference = providerRef, IdempotencyKey = key, Details = details });
    private void AddHistory(Guid request, Guid actor, FinanceApprovalStage stage, EscrowApprovalAction action, string? reason) => _db.PaymentApprovalHistory.Add(new PaymentApprovalHistory { RequestId = request, RequestType = "EscrowRelease", ActorUserId = actor, Stage = stage, Action = action, Reason = reason?.Trim() });
    private Task<bool> HasGrant(string permission, CancellationToken ct) => UserId() is { } id && IsFinance() ? _db.FinanceAccessGrants.AnyAsync(g => g.UserId == id && g.Permission == permission && g.IsActive, ct) : Task.FromResult(false);
    private Guid? UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
    private Guid? DepartmentId() => Guid.TryParse(User.FindFirstValue("departmentId"), out var id) ? id : null;
    private string Role() => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "";
    private bool IsOwner() => Role() == "Owner";
    private bool IsFinance() => string.Equals(User.FindFirstValue("departmentCode"), "FINANCE", StringComparison.OrdinalIgnoreCase);
    private bool IsFrontOffice() => User.FindFirstValue("departmentCode") is "FRONTOFFICE" or "FRONTDESK";
}
