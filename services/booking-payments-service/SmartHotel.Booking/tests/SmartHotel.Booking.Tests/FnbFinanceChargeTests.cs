using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.API.Controllers;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public sealed class FnbFinanceChargeTests
{
    private static BookingDbContext Context() =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase($"fnb-finance-tests-{Guid.NewGuid():N}")
            .Options);

    private static FinanceOperationsController Controller(
        BookingDbContext db, Guid userId, string role, string? departmentCode)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new("role", role)
        };
        if (!string.IsNullOrWhiteSpace(departmentCode))
        {
            claims.Add(new Claim("departmentCode", departmentCode));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new FinanceOperationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    [Fact]
    public async Task CreateFnbCharge_WhenAuthorizedWaiter_CreatesCustomerInvoice()
    {
        await using var db = Context();
        var waiterId = Guid.NewGuid();
        var controller = Controller(db, waiterId, "Waiter", "FOODBEVERAGE");

        var orderId = Guid.NewGuid();
        var request = new CreateFnbChargeRequest(
            orderId, "FNB-20260918-0001", "Restaurant", "Table 4",
            null, "John Silva", 3500m, 0m, 3500m, "LKR", $"fnb:order:{orderId}"
        );

        var result = await controller.CreateFnbCharge(request, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        var invoice = (FinanceInvoice)((ObjectResult)result).Value!;

        invoice.ChargeReference.Should().Be($"fnb:order:{orderId}");
        invoice.TotalAmount.Should().Be(3500m);
        invoice.Type.Should().Be(InvoiceType.Customer);
        invoice.Status.Should().Be(InvoiceStatus.Validated);

        (await db.FinanceInvoices.CountAsync()).Should().Be(1);
        (await db.FinanceAuditLogs.CountAsync(a => a.Action == "fnb.charge.invoiced")).Should().Be(1);
    }

    [Fact]
    public async Task CreateFnbCharge_WhenDuplicateOrderId_ReturnsExistingInvoiceIdempotently()
    {
        await using var db = Context();
        var waiterId = Guid.NewGuid();
        var controller = Controller(db, waiterId, "Waiter", "FOODBEVERAGE");

        var orderId = Guid.NewGuid();
        var request = new CreateFnbChargeRequest(
            orderId, "FNB-20260918-0002", "Restaurant", "Table 2",
            null, "Guest", 2000m, 100m, 2100m, "LKR", $"fnb:order:{orderId}"
        );

        // First attempt -> 201 Created
        var res1 = await controller.CreateFnbCharge(request, default);
        res1.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);

        // Second attempt -> 200 OK with identical invoice
        var res2 = await controller.CreateFnbCharge(request, default);
        res2.Should().BeOfType<OkObjectResult>();

        (await db.FinanceInvoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateFnbCharge_WhenTotalAmountMismatch_ReturnsBadRequest()
    {
        await using var db = Context();
        var controller = Controller(db, Guid.NewGuid(), "Waiter", "FOODBEVERAGE");

        var request = new CreateFnbChargeRequest(
            Guid.NewGuid(), "FNB-20260918-0003", "Restaurant", "Table 1",
            null, "Guest", 1000m, 0m, 1500m, "LKR", "idempotency-key"
        );

        var result = await controller.CreateFnbCharge(request, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateFnbCharge_WhenRoomServiceHasActiveBooking_Succeeds()
    {
        await using var db = Context();
        var booking = new Domain.Entities.Booking
        {
            BookingReference = "BK-FNB-TEST",
            CustomerId = Guid.NewGuid(),
            RoomId = Guid.NewGuid(),
            RoomNumber = "302",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = BookingStatus.CheckedIn,
            TotalAmount = 20000m
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = Controller(db, Guid.NewGuid(), "Waiter", "FOODBEVERAGE");
        var orderId = Guid.NewGuid();
        var request = new CreateFnbChargeRequest(
            orderId, "FNB-20260918-0004", "RoomService", "Room 302",
            booking.Id, "Guest", 4500m, 0m, 4500m, "LKR", $"fnb:order:{orderId}"
        );

        var result = await controller.CreateFnbCharge(request, default);
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);

        var invoice = (FinanceInvoice)((ObjectResult)result).Value!;
        invoice.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public async Task CreateFnbCharge_WhenRoomServiceBookingCancelledOrMissing_ReturnsBadRequest()
    {
        await using var db = Context();
        var controller = Controller(db, Guid.NewGuid(), "Waiter", "FOODBEVERAGE");

        var request = new CreateFnbChargeRequest(
            Guid.NewGuid(), "FNB-20260918-0005", "RoomService", "Room 999",
            Guid.NewGuid(), "Guest", 3000m, 0m, 3000m, "LKR", "key-999"
        );

        var result = await controller.CreateFnbCharge(request, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateFnbCharge_WhenCallerFromHousekeeping_ReturnsForbidden()
    {
        await using var db = Context();
        var controller = Controller(db, Guid.NewGuid(), "Housekeeper", "HOUSEKEEPING");

        var request = new CreateFnbChargeRequest(
            Guid.NewGuid(), "FNB-20260918-0006", "Restaurant", "Table 1",
            null, "Guest", 1000m, 0m, 1000m, "LKR", "key"
        );

        var result = await controller.CreateFnbCharge(request, default);
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task FnbStaff_CannotAccessGeneralInvoicesOrTransactions()
    {
        await using var db = Context();
        var controller = Controller(db, Guid.NewGuid(), "Waiter", "FOODBEVERAGE");

        (await controller.Invoices(null, default)).Should().BeOfType<ForbidResult>();
        (await controller.Transactions(null, null, null, null, default)).Should().BeOfType<ForbidResult>();
    }
}
