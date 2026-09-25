using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.Booking.API.Controllers;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.Infrastructure.Services;
using SmartHotel.Booking.Application.Interfaces;
using System.Text;

namespace SmartHotel.Booking.Tests;

public sealed class EscrowWorkflowTests
{
    private static readonly Guid FinanceDepartment = Guid.NewGuid();

    [Fact]
    public async Task FinanceEmployee_CanPrepareReleaseOnlyAfterCheckoutWithHeldFunds()
    {
        await using var db = Context();
        var payment = AddPayment(db, BookingStatus.CheckedOut, PaymentStatus.FundsHeld);
        await db.SaveChangesAsync();
        var controller = Controller(db, Guid.NewGuid(), "Employee", "FINANCE", FinanceDepartment);

        var result = await controller.PrepareRelease(new PrepareReleaseRequest(payment.Id, "release-1", FinanceDepartment, null), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        (await db.EscrowReleaseRequests.SingleAsync()).Amount.Should().Be(payment.Amount);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed, PaymentStatus.FundsHeld)]
    [InlineData(BookingStatus.CheckedOut, PaymentStatus.Completed)]
    public async Task ReleasePreparation_RejectsUnsatisfiedConditions(BookingStatus bookingStatus, PaymentStatus paymentStatus)
    {
        await using var db = Context();
        var payment = AddPayment(db, bookingStatus, paymentStatus); await db.SaveChangesAsync();
        var result = await Controller(db, Guid.NewGuid(), "Employee", "FINANCE", FinanceDepartment)
            .PrepareRelease(new PrepareReleaseRequest(payment.Id, Guid.NewGuid().ToString(), FinanceDepartment, null), default);
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task FrontOffice_CannotPrepareOrExecuteEscrowRelease()
    {
        await using var db = Context();
        var payment = AddPayment(db, BookingStatus.CheckedOut, PaymentStatus.FundsHeld); await db.SaveChangesAsync();
        var controller = Controller(db, Guid.NewGuid(), "Manager", "FRONTOFFICE", Guid.NewGuid());
        (await controller.PrepareRelease(new PrepareReleaseRequest(payment.Id, "release-x", FinanceDepartment, null), default)).Should().BeOfType<ForbidResult>();
        (await controller.ExecuteRelease(Guid.NewGuid(), new ExecuteEscrowRequest("execute-x"), default)).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task MissingProvider_FailsClosedWithoutMarkingFundsReleased()
    {
        await using var db = Context();
        var actor = Guid.NewGuid();
        var payment = AddPayment(db, BookingStatus.CheckedOut, PaymentStatus.FundsHeld);
        var release = new EscrowReleaseRequest { PaymentId = payment.Id, Payment = payment, Amount = payment.Amount, Currency = "LKR", Description = "Release", IdempotencyKey = "release-approved", SubmittedByUserId = Guid.NewGuid(), SubmittedByDepartmentId = FinanceDepartment, Status = FinanceRequestStatus.Approved, ApprovalStage = FinanceApprovalStage.Complete };
        db.AddRange(release, new FinanceAccessGrant { UserId = actor, Permission = FinancePermissions.ExecutePayments, GrantedByUserId = Guid.NewGuid(), IsActive = true }); await db.SaveChangesAsync();

        var result = await Controller(db, actor, "Employee", "FINANCE", FinanceDepartment).ExecuteRelease(release.Id, new ExecuteEscrowRequest("exec-1"), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(501);
        payment.Status.Should().Be(PaymentStatus.FundsHeld);
        release.ExecutionStatus.Should().Be(FinanceExecutionStatus.NotStarted);
    }

    [Fact]
    public async Task VerifiedRelease_Settlement_AndReconciliation_CompleteLifecycle()
    {
        await using var db = Context(); var executor = Guid.NewGuid(); var manager = Guid.NewGuid();
        var payment = AddPayment(db, BookingStatus.CheckedOut, PaymentStatus.FundsHeld);
        var release = new EscrowReleaseRequest { PaymentId = payment.Id, Payment = payment, Amount = payment.Amount, Currency = "LKR", Description = "Approved release", IdempotencyKey = "release-e2e", SubmittedByUserId = Guid.NewGuid(), SubmittedByDepartmentId = FinanceDepartment, Status = FinanceRequestStatus.Approved, ApprovalStage = FinanceApprovalStage.Complete };
        db.AddRange(release,
            new FinanceAccessGrant { UserId = executor, Permission = FinancePermissions.ExecutePayments, GrantedByUserId = Guid.NewGuid(), IsActive = true },
            new FinanceAccessGrant { UserId = manager, Permission = FinancePermissions.ReconcileSettlements, GrantedByUserId = Guid.NewGuid(), IsActive = true });
        await db.SaveChangesAsync(); var provider = new VerifiedFakeEscrowProvider(payment.ProviderPaymentReference!, payment.Amount, payment.Currency);
        var escrow = Controller(db, executor, "Employee", "FINANCE", FinanceDepartment, provider);

        (await escrow.ExecuteRelease(release.Id, new ExecuteEscrowRequest("execute-e2e"), default)).Should().BeOfType<OkObjectResult>();
        payment.Status.Should().Be(PaymentStatus.Released); release.ExecutionStatus.Should().Be(FinanceExecutionStatus.Executed);
        escrow.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        (await escrow.Webhook("valid", default)).Should().BeOfType<OkObjectResult>();
        payment.Status.Should().Be(PaymentStatus.Settled); var settlement = await db.SettlementRecords.SingleAsync();

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, manager.ToString()), new Claim(ClaimTypes.Role, "Manager"), new Claim("departmentCode", "FINANCE") };
        var operations = new FinanceOperationsController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } } };
        var prepared = (ObjectResult)await operations.Reconcile(new ReconciliationRequest(settlement.Id, payment.Amount, 10m, payment.Amount - 10m, "LKR", null), default);
        var reconciliation = (SettlementReconciliation)prepared.Value!;
        (await operations.CompleteReconciliation(reconciliation.Id, default)).Should().BeOfType<OkObjectResult>();
        reconciliation.Status.Should().Be(ReconciliationStatus.Completed);
    }

    private static BookingDbContext Context() => new(new DbContextOptionsBuilder<BookingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Payment AddPayment(BookingDbContext db, BookingStatus bookingStatus, PaymentStatus paymentStatus)
    {
        var booking = new SmartHotel.Booking.Domain.Entities.Booking { Id = Guid.NewGuid(), BookingReference = Guid.NewGuid().ToString(), CustomerId = Guid.NewGuid(), CustomerLastName = "Guest", CustomerEmail = "guest@example.test", RoomId = Guid.NewGuid(), RoomTypeId = Guid.NewGuid(), CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow), TotalAmount = 1000m, Status = bookingStatus };
        var payment = new Payment { Id = Guid.NewGuid(), BookingId = booking.Id, Booking = booking, Amount = 1000m, Currency = "LKR", Provider = PaymentProvider.Escrow, PayHereOrderId = Guid.NewGuid().ToString(), ProviderPaymentReference = Guid.NewGuid().ToString(), Status = paymentStatus };
        db.AddRange(booking, payment); return payment;
    }
    private static EscrowController Controller(BookingDbContext db, Guid user, string role, string department, Guid departmentId, IEscrowPaymentProvider? provider = null)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Role, role), new Claim("departmentCode", department), new Claim("departmentId", departmentId.ToString()) };
        return new EscrowController(db, provider ?? new UnsupportedEscrowPaymentProvider(NullLogger<UnsupportedEscrowPaymentProvider>.Instance)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } } };
    }

    private sealed class VerifiedFakeEscrowProvider(string paymentReference, decimal amount, string currency) : IEscrowPaymentProvider
    {
        public PaymentProvider Provider => PaymentProvider.Escrow;
        public EscrowProviderCapabilities Capabilities => new(true, true, true, true);
        public Task<EscrowCheckoutResult> CreateCheckoutAsync(string bookingReference, decimal amount, string currency, string idempotencyKey, CancellationToken ct) => Task.FromResult(new EscrowCheckoutResult(true, "https://sandbox.invalid", paymentReference, "checkout-test"));
        public Task<EscrowOperationResult> ReleaseAsync(string providerPaymentReference, decimal releaseAmount, string releaseCurrency, string idempotencyKey, CancellationToken ct) => Task.FromResult(new EscrowOperationResult(true, true, "release-provider-confirmed"));
        public Task<EscrowOperationResult> RefundAsync(string providerPaymentReference, decimal refundAmount, string refundCurrency, string idempotencyKey, CancellationToken ct) => Task.FromResult(new EscrowOperationResult(true, true, "refund-provider-confirmed"));
        public bool TryVerifyWebhook(string rawPayload, string signature, out EscrowWebhookEvent? webhookEvent)
        {
            webhookEvent = signature == "valid" ? new EscrowWebhookEvent("settlement-event-1", "settled", paymentReference, "settlement-provider-confirmed", amount, currency) : null;
            return webhookEvent is not null;
        }
    }
}
