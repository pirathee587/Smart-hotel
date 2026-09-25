using System.Globalization;
using System.Security.Claims;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartHotel.Authorization;
using SmartHotel.Booking.API.Controllers;
using SmartHotel.Booking.Application.Features.Payments.Commands;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.Infrastructure.Services;

namespace SmartHotel.Booking.Tests;

public sealed class FinanceSecurityAuditTests
{
    private static readonly Guid FinanceDepartment = Guid.NewGuid();

    [Fact]
    public async Task PaymentLookup_BlocksHorizontalIdor()
    {
        await using var db = Context(); var booking = Booking(Guid.NewGuid()); db.Add(booking); await db.SaveChangesAsync();
        var controller = PaymentController(db, Guid.NewGuid(), "Customer", null);
        (await controller.GetPaymentByBookingId(booking.Id, default)).Should().BeOfType<ForbidResult>();
        (await controller.CreatePayHereOrder(new CreatePayHereOrderRequest { BookingId = booking.Id }, default)).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ManualPaymentConfirmation_IsDisabledForEveryRole()
    {
        await using var db = Context();
        var result = await PaymentController(db, Guid.NewGuid(), "Owner", null).ConfirmPaymentManual(Guid.NewGuid(), new ConfirmPaymentManualDto("fake-success", null), default);
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status410Gone);
    }

    [Fact]
    public async Task SignedWebhook_WithWrongRecordedAmount_IsRejectedWithoutStateChange()
    {
        await using var db = Context(); var booking = Booking(Guid.NewGuid()); var payment = Payment(booking, 100m); db.AddRange(booking, payment); await db.SaveChangesAsync();
        var service = PayHere(); const string receivedAmount = "1.00"; var secretHash = PayHereService.ComputeMd5("audit-test-secret");
        var signature = PayHereService.ComputeMd5($"audit-merchant{booking.BookingReference}{receivedAmount}LKR2{secretHash}");
        var payload = new SmartHotel.Booking.Application.Interfaces.PayHereWebhookPayload("audit-merchant", booking.BookingReference, "provider-payment-1", receivedAmount, "LKR", 2, signature);

        var result = await new PayHereWebhookCommandHandler(db, service).Handle(new PayHereWebhookCommand(payload), default);

        result.Succeeded.Should().BeFalse(); payment.Status.Should().Be(PaymentStatus.Created); booking.Status.Should().Be(BookingStatus.PendingPayment); db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckoutOrderCreation_IsIdempotent()
    {
        await using var db = Context(); var booking = Booking(Guid.NewGuid()); db.Add(booking); await db.SaveChangesAsync(); var handler = new CreatePayHereOrderCommandHandler(db, PayHere());
        var command = new CreatePayHereOrderCommand(new CreatePayHereOrderRequest { BookingId = booking.Id });
        (await handler.Handle(command, default)).Succeeded.Should().BeTrue();
        (await handler.Handle(command, default)).Succeeded.Should().BeTrue();
        (await db.Payments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentPartialRefunds_CannotExceedPaymentAmount()
    {
        var root = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString();
        await using (var seed = Context(name, root)) { var booking = Booking(Guid.NewGuid()); var payment = Payment(booking, 100m); payment.Status = PaymentStatus.Completed; seed.AddRange(booking, payment); await seed.SaveChangesAsync(); }
        Guid paymentId; await using (var read = Context(name, root)) paymentId = await read.Payments.Select(p => p.Id).SingleAsync();
        var user1 = Guid.NewGuid(); var user2 = Guid.NewGuid();
        await using var db1 = Context(name, root); await using var db2 = Context(name, root);
        var first = FinanceController(db1, user1); var second = FinanceController(db2, user2);
        var results = await Task.WhenAll(
            first.SubmitRefund(new CreateRefundRequest(paymentId, 60m, "LKR", "Partial one", "refund-concurrent-1", FinanceDepartment), default),
            second.SubmitRefund(new CreateRefundRequest(paymentId, 60m, "LKR", "Partial two", "refund-concurrent-2", FinanceDepartment), default));

        results.Count(r => r is ObjectResult { StatusCode: 201 }).Should().Be(1);
        await using var verify = Context(name, root); (await verify.RefundRequests.SumAsync(r => r.Amount)).Should().Be(60m);
    }

    [Fact]
    public async Task CrossDepartmentGrant_DoesNotBypassFinanceIsolation()
    {
        await using var db = Context(); var attacker = Guid.NewGuid(); db.FinanceAccessGrants.Add(new FinanceAccessGrant { UserId = attacker, Permission = FinancePermissions.ViewPayroll, GrantedByUserId = Guid.NewGuid(), IsActive = true }); await db.SaveChangesAsync();
        var controller = OperationsController(db, attacker, "Manager", "HOUSEKEEPING", null);
        (await controller.Payroll(default)).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task InvalidCurrency_ReturnsBadRequest_AndAuditDetailsAreRedacted()
    {
        await using var db = Context(); var verifier = Guid.NewGuid(); db.FinanceAccessGrants.Add(new FinanceAccessGrant { UserId = verifier, Permission = FinancePermissions.VerifyExpenses, GrantedByUserId = Guid.NewGuid(), IsActive = true });
        var expense = new ExpenseRequest { Amount = 10m, Currency = "LKR", Description = "Supplies", Category = "Ops", IdempotencyKey = "expense-audit-1", SubmittedByUserId = Guid.NewGuid(), SubmittedByDepartmentId = Guid.NewGuid() }; db.Add(expense); await db.SaveChangesAsync();
        var controller = OperationsController(db, verifier, "Employee", "FINANCE", "Accountant");
        (await controller.CreateInvoice(new CreateInvoiceRequest(InvoiceType.Customer, "charge-invalid", null, null, "Guest", "g@example.test", 1m, 0m, "1234", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))), default)).Should().BeOfType<BadRequestObjectResult>();
        await controller.VerifyExpense(expense.Id, new ExpenseVerificationRequest(false, null, "token=very-secret-value"), default);
        var audit = await db.FinanceAuditLogs.SingleAsync(); audit.Details.Should().Contain("[REDACTED]").And.NotContain("very-secret-value");
    }

    [Fact]
    public void FinanceDatabaseModel_HasCriticalUniquenessAndRestrictRelationships()
    {
        using var db = Context(); var model = db.Model;
        var payment = model.FindEntityType(typeof(Payment))!;
        payment.GetIndexes().Should().Contain(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(SmartHotel.Booking.Domain.Entities.Payment.Provider), nameof(SmartHotel.Booking.Domain.Entities.Payment.PayHereOrderId) }));
        payment.GetIndexes().Should().Contain(i => i.IsUnique && i.Properties.Single().Name == nameof(SmartHotel.Booking.Domain.Entities.Payment.PayHerePaymentId));
        var refund = model.FindEntityType(typeof(RefundRequest))!;
        refund.GetForeignKeys().Single(f => f.Properties.Single().Name == nameof(RefundRequest.PaymentId)).DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        model.FindEntityType(typeof(FinanceInvoice))!.GetIndexes().Should().Contain(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(FinanceInvoice.Type), nameof(FinanceInvoice.ChargeReference) }));
        model.FindEntityType(typeof(SettlementReconciliation))!.GetIndexes().Should().Contain(i => i.IsUnique && i.Properties.Single().Name == nameof(SettlementReconciliation.SettlementId));
    }

    private static BookingDbContext Context() => Context(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
    private static BookingDbContext Context(string name, InMemoryDatabaseRoot root) => new(new DbContextOptionsBuilder<BookingDbContext>().UseInMemoryDatabase(name, root).Options);
    private static SmartHotel.Booking.Domain.Entities.Booking Booking(Guid customer) => new() { BookingReference = $"TH-AUDIT-{Guid.NewGuid():N}", CustomerId = customer, CustomerLastName = "Guest", CustomerEmail = "guest@example.test", RoomId = Guid.NewGuid(), RoomTypeId = Guid.NewGuid(), CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), TotalAmount = 100m, Currency = "LKR", Status = BookingStatus.PendingPayment };
    private static Payment Payment(SmartHotel.Booking.Domain.Entities.Booking booking, decimal amount) => new() { BookingId = booking.Id, Booking = booking, Amount = amount, Currency = "LKR", Provider = PaymentProvider.PayHere, PayHereOrderId = booking.BookingReference, Status = PaymentStatus.Created };
    private static PayHereService PayHere() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PAYHERE_MERCHANT_ID"] = "audit-merchant", ["PAYHERE_MERCHANT_SECRET"] = "audit-test-secret", ["PAYHERE_IS_SANDBOX"] = "true" }).Build(), NullLogger<PayHereService>.Instance);
    private static PaymentsController PaymentController(BookingDbContext db, Guid user, string role, string? department) => SetUser(new PaymentsController(Mock.Of<IMediator>(), db, new ServiceCollection().AddLogging().AddHotelDepartmentAuthorization().BuildServiceProvider().GetRequiredService<IAuthorizationService>()), user, role, department, null);
    private static FinanceController FinanceController(BookingDbContext db, Guid user) => SetUser(new FinanceController(db), user, "Employee", "FINANCE", "Finance Assistant", FinanceDepartment);
    private static FinanceOperationsController OperationsController(BookingDbContext db, Guid user, string role, string department, string? designation) => SetUser(new FinanceOperationsController(db), user, role, department, designation);
    private static T SetUser<T>(T controller, Guid user, string role, string? department, string? designation, Guid? departmentId = null) where T : ControllerBase
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.ToString()), new(ClaimTypes.Role, role) }; if (department is not null) claims.Add(new("departmentCode", department)); if (designation is not null) claims.Add(new("designation", designation)); if (departmentId.HasValue) claims.Add(new("departmentId", departmentId.Value.ToString()));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "audit-test")) } }; return controller;
    }
}
