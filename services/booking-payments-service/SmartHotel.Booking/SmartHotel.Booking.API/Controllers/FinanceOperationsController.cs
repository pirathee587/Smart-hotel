using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;

namespace SmartHotel.Booking.API.Controllers;

public record CreateInvoiceRequest(InvoiceType Type, string ChargeReference, Guid? BookingId, Guid? PaymentId, string PartyName, string PartyEmail, decimal Subtotal, decimal TaxAmount, string Currency, DateOnly DueDate);
public record CreateFnbChargeRequest(Guid OrderId, string OrderNumber, string OrderType, string TableOrRoomNumber, Guid? BookingId, string? CustomerName, decimal Subtotal, decimal TaxAmount, decimal TotalAmount, string Currency, string IdempotencyKey);
public record InvoiceActionRequest(string Action, decimal? PaidAmount = null);
public record CreateCreditNoteRequest(decimal Amount, string Reason);
public record ImportPayrollRequest(Guid SourcePayrollId, Guid EmployeeId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal BaseSalary, decimal Allowances, decimal Deductions, string Currency);
public record PayrollDecisionRequest(bool Approve, string? Reason);
public record ReconciliationRequest(Guid SettlementId, decimal ProviderReportedAmount, decimal ProviderFee, decimal BankReceivedAmount, string Currency, string? Notes);
public record ExpenseVerificationRequest(bool Verify, string? ReceiptUrl, string? Reason);

[ApiController]
[Route("api/v1/finance/operations")]
[Authorize]
public sealed class FinanceOperationsController : ControllerBase
{
    private readonly BookingDbContext _db;
    public FinanceOperationsController(BookingDbContext db) => _db = db;

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions([FromQuery] string? search, [FromQuery] PaymentStatus? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (!await CanViewReports(ct)) return Forbid();
        var query = _db.Payments.AsNoTracking().Include(p => p.Booking).AsQueryable();
        if (status.HasValue) query = query.Where(p => p.Status == status);
        if (from.HasValue) query = query.Where(p => p.CreatedAtUtc >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(p => p.CreatedAtUtc <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.PayHereOrderId.Contains(search) || (p.ProviderPaymentReference != null && p.ProviderPaymentReference.Contains(search)) || (p.Booking != null && p.Booking.BookingReference.Contains(search)));
        return Ok(await query.OrderByDescending(p => p.CreatedAtUtc).Take(500).Select(p => new { p.Id, p.BookingId, bookingReference = p.Booking != null ? p.Booking.BookingReference : "", p.Amount, p.Currency, p.Provider, p.Status, p.ProviderPaymentReference, p.CreatedAtUtc, p.FundsHeldAtUtc, p.ReleasedAtUtc, p.SettledAtUtc, p.RefundedAmount }).ToListAsync(ct));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices([FromQuery] InvoiceStatus? status, CancellationToken ct)
    {
        if (!await CanManageInvoices(ct)) return Forbid();
        var q = _db.FinanceInvoices.AsNoTracking(); if (status.HasValue) q = q.Where(i => i.Status == status);
        return Ok(await q.OrderByDescending(i => i.CreatedAtUtc).Take(500).ToListAsync(ct));
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest request, CancellationToken ct)
    {
        if (!await CanManageInvoices(ct)) return Forbid();
        if (request.Subtotal < 0 || request.TaxAmount < 0 || !IsCurrency(request.Currency) || request.DueDate < DateOnly.FromDateTime(DateTime.UtcNow) || string.IsNullOrWhiteSpace(request.ChargeReference)) return BadRequest(new { message = "Valid charge reference, ISO currency, amounts and due date are required." });
        var existing = await _db.FinanceInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Type == request.Type && i.ChargeReference == request.ChargeReference, ct); if (existing is not null) return Ok(existing);
        var invoice = new FinanceInvoice { InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", Type = request.Type, ChargeReference = request.ChargeReference.Trim(), BookingId = request.BookingId, PaymentId = request.PaymentId, PartyName = request.PartyName.Trim(), PartyEmail = request.PartyEmail.Trim(), Subtotal = request.Subtotal, TaxAmount = request.TaxAmount, TotalAmount = request.Subtotal + request.TaxAmount, Currency = Currency(request.Currency), IssueDate = DateOnly.FromDateTime(DateTime.UtcNow), DueDate = request.DueDate, CreatedByUserId = UserId()!.Value };
        _db.FinanceInvoices.Add(invoice); Audit("invoice.created", nameof(FinanceInvoice), invoice.Id, $"Invoice {invoice.InvoiceNumber} for {invoice.TotalAmount} {invoice.Currency}");
        try { await _db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new { message = "This charge has already been invoiced." }); }
        return StatusCode(201, invoice);
    }

    [HttpPost("charges/fnb")]
    public async Task<IActionResult> CreateFnbCharge(CreateFnbChargeRequest request, CancellationToken ct)
    {
        if (!CanSubmitFnbCharge()) return Forbid();

        if (request.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.OrderNumber))
            return BadRequest(new { message = "Valid OrderId and OrderNumber are required." });

        if (request.Subtotal < 0 || request.TaxAmount < 0 || request.TotalAmount != (request.Subtotal + request.TaxAmount) || !IsCurrency(request.Currency))
            return BadRequest(new { message = "Amounts must be non-negative, total must equal subtotal plus tax, and valid ISO currency is required." });

        var chargeReference = $"fnb:order:{request.OrderId}";

        // Room-service booking verification: if bookingId is provided, independently verify in Booking DB
        if (request.BookingId.HasValue)
        {
            var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BookingId.Value, ct);
            if (booking == null || (booking.Status != BookingStatus.Confirmed && booking.Status != BookingStatus.CheckedIn))
            {
                return BadRequest(new { message = "Room-service charge must reference an active, confirmed or checked-in reservation." });
            }
        }

        // Idempotency: Check if an invoice with this chargeReference already exists
        var existing = await _db.FinanceInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Type == InvoiceType.Customer && i.ChargeReference == chargeReference, ct);
        if (existing != null)
        {
            return Ok(existing);
        }

        var partyName = !string.IsNullOrWhiteSpace(request.CustomerName)
            ? request.CustomerName.Trim()
            : (!string.IsNullOrWhiteSpace(request.TableOrRoomNumber) ? request.TableOrRoomNumber.Trim() : "F&B Guest");

        var invoice = new FinanceInvoice
        {
            InvoiceNumber = $"INV-FNB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Type = InvoiceType.Customer,
            ChargeReference = chargeReference,
            BookingId = request.BookingId,
            PaymentId = null,
            PartyName = partyName,
            PartyEmail = "fnb-orders@smarthotel.local",
            Subtotal = request.Subtotal,
            TaxAmount = request.TaxAmount,
            TotalAmount = request.TotalAmount,
            Currency = Currency(request.Currency),
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Status = InvoiceStatus.Validated,
            CreatedByUserId = UserId() ?? Guid.Empty
        };

        _db.FinanceInvoices.Add(invoice);
        Audit("fnb.charge.invoiced", nameof(FinanceInvoice), invoice.Id, $"F&B Order {request.OrderNumber} for {invoice.TotalAmount} {invoice.Currency} (Ref: {chargeReference})");

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var raceExisting = await _db.FinanceInvoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Type == InvoiceType.Customer && i.ChargeReference == chargeReference, ct);
            if (raceExisting != null) return Ok(raceExisting);
            return Conflict(new { message = "This F&B charge has already been invoiced." });
        }

        return StatusCode(201, invoice);
    }

    [HttpPost("invoices/{id:guid}/action")]
    public async Task<IActionResult> InvoiceAction(Guid id, InvoiceActionRequest request, CancellationToken ct)
    {
        if (!await CanManageInvoices(ct)) return Forbid(); var invoice = await _db.FinanceInvoices.FirstOrDefaultAsync(i => i.Id == id, ct); if (invoice is null) return NotFound();
        switch (request.Action.Trim().ToLowerInvariant())
        {
            case "validate" when invoice.Status == InvoiceStatus.Draft: invoice.Status = InvoiceStatus.Validated; invoice.ValidatedByUserId = UserId(); break;
            case "issue" when invoice.Status == InvoiceStatus.Validated: invoice.Status = InvoiceStatus.Issued; invoice.IssuedAtUtc = DateTime.UtcNow; break;
            case "record-payment" when invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid:
                if (!request.PaidAmount.HasValue || request.PaidAmount <= 0 || invoice.PaidAmount + request.PaidAmount > invoice.TotalAmount) return BadRequest(new { message = "Payment exceeds invoice outstanding amount." });
                invoice.PaidAmount += request.PaidAmount.Value; invoice.Status = invoice.PaidAmount == invoice.TotalAmount ? InvoiceStatus.Settled : InvoiceStatus.PartiallyPaid; break;
            default: return Conflict(new { message = "Invalid invoice state transition." });
        }
        invoice.UpdatedAtUtc = DateTime.UtcNow; Audit($"invoice.{request.Action.ToLowerInvariant()}", nameof(FinanceInvoice), invoice.Id, $"Status {invoice.Status}"); await _db.SaveChangesAsync(ct); return Ok(invoice);
    }

    [HttpPost("invoices/{id:guid}/credit-notes")]
    public async Task<IActionResult> CreditNote(Guid id, CreateCreditNoteRequest request, CancellationToken ct)
    {
        if (!IsOwner()) return Forbid(); var invoice = await _db.FinanceInvoices.FirstOrDefaultAsync(i => i.Id == id, ct); if (invoice is null) return NotFound();
        var prior = await _db.FinanceCreditNotes.Where(c => c.InvoiceId == id && c.Status != CreditNoteStatus.Cancelled).SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        if (request.Amount <= 0 || request.Amount + prior > invoice.TotalAmount) return BadRequest(new { message = "Credit notes cannot exceed invoice total." });
        var note = new FinanceCreditNote { InvoiceId = id, CreditNoteNumber = $"CN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", Amount = request.Amount, Currency = invoice.Currency, Reason = request.Reason.Trim(), Status = CreditNoteStatus.Authorized, CreatedByUserId = UserId()!.Value, AuthorizedByUserId = UserId() };
        _db.FinanceCreditNotes.Add(note); Audit("credit-note.authorized", nameof(FinanceCreditNote), note.Id, $"Invoice {invoice.InvoiceNumber}; amount {note.Amount}"); await _db.SaveChangesAsync(ct); return StatusCode(201, note);
    }

    [HttpGet("invoices/{id:guid}/pdf")]
    public async Task<IActionResult> InvoicePdf(Guid id, CancellationToken ct)
    {
        if (!await CanManageInvoices(ct) && !IsOwner()) return Forbid(); var i = await _db.FinanceInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct); if (i is null) return NotFound();
        return File(SimplePdf.Create($"SmartHotel Invoice {i.InvoiceNumber}\nParty: {i.PartyName}\nIssue: {i.IssueDate}\nDue: {i.DueDate}\nSubtotal: {i.Subtotal:0.00} {i.Currency}\nTax: {i.TaxAmount:0.00} {i.Currency}\nTotal: {i.TotalAmount:0.00} {i.Currency}\nStatus: {i.Status}"), "application/pdf", $"{i.InvoiceNumber}.pdf");
    }

    [HttpGet("payroll")]
    public async Task<IActionResult> Payroll(CancellationToken ct)
    {
        if (!IsOwner() && !await HasGrant(FinancePermissions.ViewPayroll, ct)) return Forbid();
        return Ok(await _db.FinancePayrollRecords.AsNoTracking().OrderByDescending(p => p.PeriodEnd).Take(500).ToListAsync(ct));
    }

    [HttpPost("payroll/import")]
    public async Task<IActionResult> ImportPayroll(ImportPayrollRequest request, CancellationToken ct)
    {
        if (!await HasGrant(FinancePermissions.SubmitPayroll, ct)) return Forbid();
        if (request.PeriodEnd < request.PeriodStart || request.BaseSalary < 0 || request.Allowances < 0 || request.Deductions < 0 || !IsCurrency(request.Currency)) return BadRequest();
        var existing = await _db.FinancePayrollRecords.AsNoTracking().FirstOrDefaultAsync(p => p.SourcePayrollId == request.SourcePayrollId, ct); if (existing is not null) return Ok(existing);
        var net = request.BaseSalary + request.Allowances - request.Deductions; if (net < 0) return BadRequest(new { message = "Net salary cannot be negative." });
        var payroll = new FinancePayrollRecord { SourcePayrollId = request.SourcePayrollId, EmployeeId = request.EmployeeId, PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd, BaseSalary = request.BaseSalary, Allowances = request.Allowances, Deductions = request.Deductions, NetSalary = net, Currency = Currency(request.Currency), SubmittedByUserId = UserId()!.Value };
        _db.FinancePayrollRecords.Add(payroll); Audit("payroll.imported", nameof(FinancePayrollRecord), payroll.Id, $"Source payroll {request.SourcePayrollId}"); await _db.SaveChangesAsync(ct); return StatusCode(201, payroll);
    }

    [HttpPost("payroll/{id:guid}/decision")]
    public async Task<IActionResult> PayrollDecision(Guid id, PayrollDecisionRequest request, CancellationToken ct)
    {
        var payroll = await _db.FinancePayrollRecords.FirstOrDefaultAsync(p => p.Id == id, ct); if (payroll is null) return NotFound();
        if (payroll.SubmittedByUserId == UserId()) return Forbid();
        if (payroll.Status == PayrollApprovalStatus.PendingVerification)
        {
            if (!await HasGrant(FinancePermissions.ViewPayroll, ct)) return Forbid(); payroll.VerifiedByUserId = UserId(); payroll.Status = request.Approve ? PayrollApprovalStatus.ManagerReview : PayrollApprovalStatus.Rejected;
        }
        else if (payroll.Status == PayrollApprovalStatus.ManagerReview)
        {
            if (!IsFinanceManager() || !await HasGrant(FinancePermissions.ApprovePayroll, ct)) return Forbid(); payroll.ManagerApprovedByUserId = UserId(); payroll.Status = request.Approve ? PayrollApprovalStatus.OwnerApproval : PayrollApprovalStatus.Rejected;
        }
        else if (payroll.Status == PayrollApprovalStatus.OwnerApproval)
        {
            if (!IsOwner()) return Forbid(); payroll.OwnerApprovedByUserId = UserId(); payroll.Status = request.Approve ? PayrollApprovalStatus.Approved : PayrollApprovalStatus.Rejected;
        }
        else return Conflict(new { message = "Payroll is not awaiting a decision." });
        payroll.UpdatedAtUtc = DateTime.UtcNow; Audit(request.Approve ? "payroll.approved-stage" : "payroll.rejected", nameof(FinancePayrollRecord), payroll.Id, $"Status {payroll.Status}; {request.Reason}"); await _db.SaveChangesAsync(ct); return Ok(payroll);
    }

    [HttpPost("payroll/{id:guid}/disburse")]
    public async Task<IActionResult> DisbursePayroll(Guid id, CancellationToken ct)
    {
        if (!await HasGrant(FinancePermissions.ExecutePayments, ct)) return Forbid();
        var payroll = await _db.FinancePayrollRecords.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct); if (payroll is null) return NotFound();
        if (payroll.Status != PayrollApprovalStatus.Approved) return Conflict(new { message = "Owner-approved payroll is required." });
        return StatusCode(501, new { message = "No approved salary-transfer provider is configured; payroll remains approved and no transfer was recorded." });
    }

    [HttpGet("payroll/{id:guid}/payslip")]
    public async Task<IActionResult> Payslip(Guid id, CancellationToken ct)
    {
        var p = await _db.FinancePayrollRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct); if (p is null) return NotFound();
        if (!IsOwner() && UserId() != p.EmployeeId && !await HasGrant(FinancePermissions.ViewPayroll, ct)) return Forbid();
        if (UserId() == p.EmployeeId && p.Status != PayrollApprovalStatus.Disbursed) return Conflict(new { message = "The employee payslip is available after confirmed simulated payment." });
        return File(SimplePdf.Create($"SmartHotel Payslip\nEmployee: {p.EmployeeId}\nPeriod: {p.PeriodStart} - {p.PeriodEnd}\nBasic salary: {p.BaseSalary:0.00} {p.Currency}\nAllowances: {p.Allowances:0.00} {p.Currency}\nApproved overtime: {p.OvertimeHours:0.##}h / {p.OvertimePay:0.00} {p.Currency}\nGross salary: {p.GrossSalary:0.00} {p.Currency}\nAuthorized deductions: {p.Deductions:0.00} {p.Currency}\nNet salary: {p.NetSalary:0.00} {p.Currency}\nStatus: {p.Status}"), "application/pdf", $"payslip-{p.PeriodEnd}-{p.EmployeeId}.pdf");
    }

    [HttpPost("reconciliations")]
    public async Task<IActionResult> Reconcile(ReconciliationRequest request, CancellationToken ct)
    {
        if (!await HasGrant(FinancePermissions.ReconcileSettlements, ct)) return Forbid(); if (!IsCurrency(request.Currency)) return BadRequest(new { message = "ISO currency is required." }); var settlement = await _db.SettlementRecords.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SettlementId && s.Status == SettlementStatus.Confirmed, ct); if (settlement is null) return BadRequest(new { message = "A verified settlement is required." });
        var existing = await _db.SettlementReconciliations.AsNoTracking().FirstOrDefaultAsync(r => r.SettlementId == request.SettlementId, ct); if (existing is not null) return Ok(existing);
        var expectedBank = request.ProviderReportedAmount - request.ProviderFee; var difference = request.BankReceivedAmount - expectedBank;
        var rec = new SettlementReconciliation { SettlementId = request.SettlementId, ProviderReportedAmount = request.ProviderReportedAmount, ProviderFee = request.ProviderFee, BankReceivedAmount = request.BankReceivedAmount, DifferenceAmount = difference, Currency = Currency(request.Currency), Status = difference == 0 ? ReconciliationStatus.ManagerReview : ReconciliationStatus.DifferencesFound, Notes = request.Notes?.Trim(), PreparedByUserId = UserId()!.Value };
        _db.SettlementReconciliations.Add(rec); Audit("reconciliation.prepared", nameof(SettlementReconciliation), rec.Id, $"Difference {difference} {rec.Currency}"); await _db.SaveChangesAsync(ct); return StatusCode(201, rec);
    }

    [HttpPost("reconciliations/{id:guid}/complete")]
    public async Task<IActionResult> CompleteReconciliation(Guid id, CancellationToken ct)
    {
        if (!IsFinanceManager() || !await HasGrant(FinancePermissions.ReconcileSettlements, ct)) return Forbid(); var rec = await _db.SettlementReconciliations.FirstOrDefaultAsync(r => r.Id == id, ct); if (rec is null) return NotFound();
        if (rec.DifferenceAmount != 0) return Conflict(new { message = "Resolve the settlement difference before completion." });
        rec.Status = ReconciliationStatus.Completed; rec.ReviewedByUserId = UserId(); rec.CompletedAtUtc = DateTime.UtcNow; Audit("reconciliation.completed", nameof(SettlementReconciliation), rec.Id, "Bank and provider amounts matched"); await _db.SaveChangesAsync(ct); return Ok(rec);
    }

    [HttpPost("expenses/{id:guid}/verify")]
    public async Task<IActionResult> VerifyExpense(Guid id, ExpenseVerificationRequest request, CancellationToken ct)
    {
        if (!await HasGrant(FinancePermissions.VerifyExpenses, ct)) return Forbid(); var expense = await _db.ExpenseRequests.FirstOrDefaultAsync(e => e.Id == id, ct); if (expense is null) return NotFound();
        if (expense.SubmittedByUserId == UserId()) return Forbid(); expense.ReceiptUrl = request.ReceiptUrl?.Trim(); expense.VerificationStatus = request.Verify ? ExpenseVerificationStatus.Verified : ExpenseVerificationStatus.Rejected; expense.VerifiedByUserId = UserId(); expense.VerifiedAtUtc = DateTime.UtcNow;
        if (!request.Verify) { expense.Status = FinanceRequestStatus.Rejected; expense.DecisionReason = request.Reason; }
        Audit(request.Verify ? "expense.verified" : "expense.verification-rejected", nameof(ExpenseRequest), expense.Id, request.Reason ?? "Document review"); await _db.SaveChangesAsync(ct); return Ok(expense);
    }

    [HttpPost("expenses/{id:guid}/execute")]
    public async Task<IActionResult> ExecuteExpense(Guid id, CancellationToken ct)
    {
        if (!await HasGrant(FinancePermissions.ExecutePayments, ct)) return Forbid(); var expense = await _db.ExpenseRequests.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct); if (expense is null) return NotFound();
        if (expense.Status != FinanceRequestStatus.Approved || expense.VerificationStatus != ExpenseVerificationStatus.Verified) return Conflict(new { message = "Verified and fully approved expense is required." });
        return StatusCode(501, new { message = "No authorized supplier-payment integration is configured; expense remains approved but unexecuted." });
    }

    [HttpGet("reports/summary")]
    public async Task<IActionResult> Report([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (!await CanViewReports(ct)) return Forbid(); var start = from?.ToUniversalTime() ?? DateTime.UtcNow.Date.AddDays(-30); var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var payments = _db.Payments.AsNoTracking().Where(p => p.CreatedAtUtc >= start && p.CreatedAtUtc <= end);
        return Ok(new { from = start, to = end, verifiedCollectedRevenue = await payments.Where(p => p.Status != PaymentStatus.Created && p.Status != PaymentStatus.Failed).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m, refunds = await payments.SumAsync(p => (decimal?)p.RefundedAmount, ct) ?? 0m, heldFunds = await payments.Where(p => p.Status == PaymentStatus.FundsHeld || p.Status == PaymentStatus.ReleasePending).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m, settledFunds = await _db.SettlementRecords.Where(s => s.Status == SettlementStatus.Confirmed && s.ConfirmedAtUtc >= start && s.ConfirmedAtUtc <= end).SumAsync(s => (decimal?)s.Amount, ct) ?? 0m, approvedExpenses = await _db.ExpenseRequests.Where(e => e.Status == FinanceRequestStatus.Approved && e.CreatedAtUtc >= start && e.CreatedAtUtc <= end).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m, approvedPayroll = await _db.FinancePayrollRecords.Where(p => (p.Status == PayrollApprovalStatus.Approved || p.Status == PayrollApprovalStatus.Disbursed) && p.CreatedAtUtc >= start && p.CreatedAtUtc <= end).SumAsync(p => (decimal?)p.NetSalary, ct) ?? 0m, definition = "Operational financial summary from verified records; this is not an accounting profit-and-loss statement." });
    }

    [HttpGet("reports/export.csv")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!await CanViewReports(ct)) return Forbid(); var rows = await _db.Payments.AsNoTracking().OrderByDescending(p => p.CreatedAtUtc).Take(5000).ToListAsync(ct); var csv = new StringBuilder("PaymentId,BookingId,Amount,Currency,Status,Provider,CreatedAtUtc\r\n");
        foreach (var p in rows) csv.Append(Csv(p.Id)).Append(',').Append(Csv(p.BookingId)).Append(',').Append(p.Amount.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Csv(p.Currency)).Append(',').Append(Csv(p.Status)).Append(',').Append(Csv(p.Provider)).Append(',').Append(Csv(p.CreatedAtUtc.ToString("O"))).Append("\r\n");
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"finance-transactions-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("reports/export.xls")]
    public async Task<IActionResult> ExportExcel(CancellationToken ct)
    {
        if (!await CanViewReports(ct)) return Forbid();
        var rows = await _db.Payments.AsNoTracking().OrderByDescending(p => p.CreatedAtUtc).Take(5000).ToListAsync(ct);
        var html = new StringBuilder("<table><tr><th>Payment</th><th>Booking</th><th>Amount</th><th>Currency</th><th>Status</th><th>Provider</th><th>Created UTC</th></tr>");
        foreach (var p in rows) html.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(p.Id.ToString())).Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(p.BookingId.ToString())).Append("</td><td>").Append(p.Amount.ToString(CultureInfo.InvariantCulture)).Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(p.Currency)).Append("</td><td>").Append(p.Status).Append("</td><td>").Append(p.Provider).Append("</td><td>").Append(p.CreatedAtUtc.ToString("O")).Append("</td></tr>");
        html.Append("</table>");
        return File(Encoding.UTF8.GetBytes(html.ToString()), "application/vnd.ms-excel", $"finance-transactions-{DateTime.UtcNow:yyyyMMdd}.xls");
    }

    private async Task<bool> CanViewReports(CancellationToken ct) => IsOwner() || IsFinance() && (Role() == "Manager" || await HasGrant(FinancePermissions.ViewReports, ct));
    private async Task<bool> CanManageInvoices(CancellationToken ct) => IsOwner() || IsFinance() && (Designation() is "Accountant" or "Finance Assistant" || await HasGrant(FinancePermissions.ManageInvoices, ct));
    private Task<bool> HasGrant(string permission, CancellationToken ct) => UserId() is { } id && IsFinance() ? _db.FinanceAccessGrants.AnyAsync(g => g.UserId == id && g.Permission == permission && g.IsActive, ct) : Task.FromResult(false);
    private void Audit(string action, string entity, Guid id, string details) => _db.FinanceAuditLogs.Add(new FinanceAuditLog { ActorUserId = UserId()!.Value, Action = action, EntityType = entity, EntityId = id, Details = FinanceAuditSanitizer.Redact(details) });
    private Guid? UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
    private string Role() => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "";
    private string Designation() => User.FindFirstValue("designation") ?? "";
    private bool IsOwner() => Role() == "Owner";
    private bool IsFinance() => string.Equals(User.FindFirstValue("departmentCode"), "FINANCE", StringComparison.OrdinalIgnoreCase);
    private bool IsFinanceManager() => IsFinance() && Role() == "Manager";
    private bool IsFnb() => string.Equals(User.FindFirstValue("departmentCode"), "FOODBEVERAGE", StringComparison.OrdinalIgnoreCase);
    private bool CanSubmitFnbCharge() => IsOwner() || (IsFnb() && Role() is "Admin" or "Manager" or "Waiter") || (IsFinance() && Role() is "Admin" or "Manager");
    private static string Currency(string value) { var c = value?.Trim().ToUpperInvariant(); if (c?.Length != 3) throw new ArgumentException("Currency must be a 3-letter ISO code."); return c; }
    private static bool IsCurrency(string? value) => value?.Trim().Length == 3 && value.Trim().All(char.IsLetter);
    private static string Csv(object? value) => $"\"{value?.ToString()?.Replace("\"", "\"\"")}\"";
}

internal static class FinanceAuditSanitizer
{
    private static readonly System.Text.RegularExpressions.Regex Sensitive = new(
        @"(?i)\b(password|secret|token|authorization|card|cvv|account|iban)\s*[:=]\s*[^;\s,]+",
        System.Text.RegularExpressions.RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));
    public static string Redact(string value) => Sensitive.Replace(value, "$1=[REDACTED]");
}

internal static class SimplePdf
{
    public static byte[] Create(string text)
    {
        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace("\r", "");
        var lines = text.Split('\n'); var content = new StringBuilder("BT /F1 11 Tf 50 790 Td ");
        foreach (var line in lines) content.Append('(').Append(Escape(line)).Append(") Tj 0 -18 Td "); content.Append("ET");
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>", $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
        var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Length; i++) { offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString())); pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n"); }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString()); pdf.Append("xref\n0 6\n0000000000 65535 f \n"); for (var i = 1; i <= 5; i++) pdf.Append(offsets[i].ToString("D10")).Append(" 00000 n \n"); pdf.Append("trailer << /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
