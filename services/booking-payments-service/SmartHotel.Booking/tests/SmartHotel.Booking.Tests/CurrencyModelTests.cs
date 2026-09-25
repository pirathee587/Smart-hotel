using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Features.Payments.Commands;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class CurrencyModelTests
{
    private const string MerchantId = "test-merchant-id";
    private const string MerchantSecret = "test-secret-key";

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;
        return new BookingDbContext(options);
    }

    private static PayHereService CreatePayHereService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PAYHERE_MERCHANT_ID"] = MerchantId,
                ["PAYHERE_MERCHANT_SECRET"] = MerchantSecret,
                ["PAYHERE_IS_SANDBOX"] = "true"
            })
            .Build();
        return new PayHereService(config, NullLogger<PayHereService>.Instance);
    }

    private static PayHereWebhookPayload CreateSignedPayload(
        string orderId,
        string amount,
        string currency,
        int statusCode,
        string paymentId)
    {
        var hashedSecret = PayHereService.ComputeMd5(MerchantSecret);
        var rawSig = $"{MerchantId}{orderId}{amount}{currency}{statusCode}{hashedSecret}";
        var signature = PayHereService.ComputeMd5(rawSig);

        return new PayHereWebhookPayload(
            MerchantId: MerchantId,
            OrderId: orderId,
            PaymentId: paymentId,
            PayHereAmount: amount,
            PayHereCurrency: currency,
            StatusCode: statusCode,
            Md5Sig: signature
        );
    }

    // ── 2. BookingDraft snapshots HotelOps currency ─────────────────────────
    [Fact]
    public async Task BookingDraft_SnapshotsHotelOpsCurrency_OnCreation()
    {
        using var context = CreateDbContext();
        var roomTypeId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        var mockHotelOps = new Mock<IHotelOpsClient>();
        mockHotelOps.Setup(x => x.GetRoomAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomInfo(roomId, roomTypeId, "101", 1, "Available"));
        mockHotelOps.Setup(x => x.GetRoomTypeAsync(roomTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomTypeInfo(roomTypeId, "Deluxe Ocean Suite", 200m, 20m, 10m, 2, true, true, Currency: "LKR"));

        var handler = new CreateDraftCommandHandler(context, mockHotelOps.Object);
        var command = new CreateDraftCommand(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "guest@example.com",
            RoomId: roomId,
            RoomName: "Deluxe Ocean Suite",
            RoomTypeId: roomTypeId,
            RatePlanId: "STD",
            RatePlanName: "Standard",
            CheckInDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            GuestCount: 2,
            RoomsCount: 1,
            PricePerNight: 200m
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Currency.Should().Be("LKR");

        var draftInDb = await context.BookingDrafts.FirstOrDefaultAsync(d => d.Id == result.Data.Id);
        draftInDb.Should().NotBeNull();
        draftInDb!.Currency.Should().Be("LKR");
    }

    // ── 3. Client cannot override booking currency ───────────────────────────
    [Fact]
    public void CreateDraftCommand_DoesNotAcceptCurrencyFromClient()
    {
        // Enforce by contract: CreateDraftCommand has no Currency property.
        // It strictly sources currency from HotelOps.
        var properties = typeof(CreateDraftCommand).GetProperties();
        properties.Should().NotContain(p => p.Name.Equals("Currency", StringComparison.OrdinalIgnoreCase));
    }

    // ── 4. Missing HotelOps currency fails closed ────────────────────────────
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData(null)]
    public async Task CreateDraft_MissingOrInvalidHotelOpsCurrency_FailsClosed(string? invalidCurrency)
    {
        using var context = CreateDbContext();
        var roomTypeId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        var mockHotelOps = new Mock<IHotelOpsClient>();
        mockHotelOps.Setup(x => x.GetRoomAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomInfo(roomId, roomTypeId, "101", 1, "Available"));
        mockHotelOps.Setup(x => x.GetRoomTypeAsync(roomTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomTypeInfo(roomTypeId, "Deluxe Ocean Suite", 200m, 20m, 10m, 2, true, true, Currency: invalidCurrency!));

        var handler = new CreateDraftCommandHandler(context, mockHotelOps.Object);
        var command = new CreateDraftCommand(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "guest@example.com",
            RoomId: roomId,
            RoomName: "Deluxe Ocean Suite",
            RoomTypeId: roomTypeId,
            RatePlanId: "STD",
            RatePlanName: "Standard",
            CheckInDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            GuestCount: 2,
            RoomsCount: 1,
            PricePerNight: 200m
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("currency is missing or invalid");
        (await context.BookingDrafts.CountAsync()).Should().Be(0);
    }

    // ── 5. Booking copies Draft currency at checkout ─────────────────────────
    // ── 6. Checkout no longer hardcodes USD ──────────────────────────────────
    // ── 7. CheckoutResultDto returns persisted currency ─────────────────────
    [Fact]
    public async Task Checkout_CopiesDraftCurrency_PassesCurrencyToGateway_AndReturnsInResult()
    {
        using var context = CreateDbContext();
        var guestId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = guestId,
            CustomerEmail = "guest@example.com",
            RoomId = roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = roomTypeId,
            RatePlanId = "STD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "Draft"
        };
        draft.RecalculateTotals();
        context.BookingDrafts.Add(draft);
        await context.SaveChangesAsync();

        var mockHotelOps = new Mock<IHotelOpsClient>();
        mockHotelOps.Setup(x => x.GetRoomAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomInfo(roomId, roomTypeId, "101", 1, "Available"));
        mockHotelOps.Setup(x => x.GetRoomTypeAsync(roomTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomTypeInfo(roomTypeId, "Ocean Suite", 200m, 20m, 10m, 2, true, true, Currency: "LKR"));

        string capturedCurrency = string.Empty;
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(x => x.ProcessTokenizedPaymentAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, string, CancellationToken>((token, amount, currency, refCode, ct) =>
            {
                capturedCurrency = currency;
            })
            .ReturnsAsync(new PaymentProcessResult(PaymentExecutionOutcome.Success, "TXN-TEST-123"));

        var handler = new CheckoutCommandHandler(context, mockGateway.Object, mockHotelOps.Object);
        var request = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Alice", "Smith", "+94771234567", "guest@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_test_card",
            CouponCode: null
        );

        var result = await handler.Handle(new CheckoutCommand(request, guestId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Currency.Should().Be("LKR");

        // Proof that Checkout no longer hardcodes USD
        capturedCurrency.Should().Be("LKR");
        capturedCurrency.Should().NotBe("USD");

        // Verify Booking entity in DB copied Draft currency
        var bookingInDb = await context.Bookings.FirstOrDefaultAsync(b => b.Id == result.Data.BookingId);
        bookingInDb.Should().NotBeNull();
        bookingInDb!.Currency.Should().Be("LKR");
    }

    [Fact]
    public async Task Checkout_DraftMissingCurrency_FailsClosed()
    {
        using var context = CreateDbContext();
        var guestId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        // Draft with null currency
        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = guestId,
            CustomerEmail = "guest@example.com",
            RoomId = roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = roomTypeId,
            RatePlanId = "STD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = null,
            Status = "Draft"
        };
        draft.RecalculateTotals();
        context.BookingDrafts.Add(draft);
        await context.SaveChangesAsync();

        var mockHotelOps = new Mock<IHotelOpsClient>();
        mockHotelOps.Setup(x => x.GetRoomAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomInfo(roomId, roomTypeId, "101", 1, "Available"));
        mockHotelOps.Setup(x => x.GetRoomTypeAsync(roomTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomTypeInfo(roomTypeId, "Ocean Suite", 200m, 20m, 10m, 2, true, true, Currency: "LKR"));

        var mockGateway = new Mock<IPaymentGateway>();
        var handler = new CheckoutCommandHandler(context, mockGateway.Object, mockHotelOps.Object);
        var request = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Alice", "Smith", "+94771234567", "guest@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_test_card",
            CouponCode: null
        );

        var result = await handler.Handle(new CheckoutCommand(request, guestId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("missing or invalid currency");
        mockGateway.Verify(x => x.ProcessTokenizedPaymentAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── 8. Payment creation copies Booking.Currency ──────────────────────────
    // ── 9. PayHere order uses Booking.Currency ──────────────────────────────
    [Fact]
    public async Task CreatePayHereOrder_CopiesBookingCurrency_ToPaymentAndPayHereOrder()
    {
        using var context = CreateDbContext();
        var booking = new SmartHotel.Booking.Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-TESTLKR",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Perera",
            CustomerEmail = "perera@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            TotalAmount = 45000m,
            Currency = "LKR",
            Status = BookingStatus.PendingPayment
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        PayHereOrderRequest? capturedPayHereReq = null;
        var mockPayHere = new Mock<IPayHereService>();
        mockPayHere.Setup(x => x.GenerateOrderRequest(It.IsAny<PayHereOrderRequest>()))
            .Callback<PayHereOrderRequest>(req => capturedPayHereReq = req)
            .Returns(new PayHereOrderResponse(
                MerchantId: MerchantId,
                OrderId: booking.BookingReference,
                Amount: 45000m,
                Currency: "LKR",
                Hash: "dummy-hash",
                CheckoutUrl: "https://sandbox.payhere.lk/pay/checkout",
                FormFields: new Dictionary<string, string>()));

        var handler = new CreatePayHereOrderCommandHandler(context, mockPayHere.Object);
        var command = new CreatePayHereOrderCommand(new CreatePayHereOrderRequest
        {
            BookingId = booking.Id,
            CustomerFirstName = "Sunil",
            CustomerPhone = "+94771234567"
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        capturedPayHereReq.Should().NotBeNull();
        capturedPayHereReq!.Currency.Should().Be("LKR");
        capturedPayHereReq.Amount.Should().Be(45000m);

        var payment = await context.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id);
        payment.Should().NotBeNull();
        payment!.Currency.Should().Be("LKR");
        payment.Amount.Should().Be(booking.TotalAmount);
    }

    [Fact]
    public async Task CreatePayHereOrder_MissingBookingCurrency_FailsClosed()
    {
        using var context = CreateDbContext();
        var booking = new SmartHotel.Booking.Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-NOCURRENCY",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Perera",
            CustomerEmail = "perera@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            TotalAmount = 45000m,
            Currency = null, // Missing!
            Status = BookingStatus.PendingPayment
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var mockPayHere = new Mock<IPayHereService>();
        var handler = new CreatePayHereOrderCommandHandler(context, mockPayHere.Object);
        var command = new CreatePayHereOrderCommand(new CreatePayHereOrderRequest
        {
            BookingId = booking.Id
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("missing or invalid authoritative currency");
        (await context.Payments.CountAsync()).Should().Be(0);
    }

    // ── 10. Webhook validates Booking.TotalAmount + Booking.Currency ─────────
    // ── 11. Currency mismatch rejects confirmation ──────────────────────────
    // ── 12. Amount mismatch rejects confirmation ────────────────────────────
    [Fact]
    public async Task Webhook_DirectBookingValidation_ValidAmountAndCurrency_ConfirmsBooking()
    {
        await using var db = CreateDbContext();
        var booking = new SmartHotel.Booking.Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-WEBHOOK-VALID",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Guest",
            CustomerEmail = "guest@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            TotalAmount = 50000m,
            Currency = "LKR",
            Status = BookingStatus.PendingPayment
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "50000.00", "LKR", 2, "PAY_VAL_001");
        var handler = new PayHereWebhookCommandHandler(db, service);

        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        result.Succeeded.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        db.OutboxMessages.Should().Contain(m => m.Type == "booking.confirmed");
    }

    [Fact]
    public async Task Webhook_DirectBookingValidation_CurrencyMismatch_RejectsConfirmation()
    {
        await using var db = CreateDbContext();
        var booking = new SmartHotel.Booking.Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-WEBHOOK-MISMATCH-CUR",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Guest",
            CustomerEmail = "guest@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            TotalAmount = 50000m,
            Currency = "LKR",
            Status = BookingStatus.PendingPayment
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        // Provider sends USD instead of LKR
        var payload = CreateSignedPayload(booking.BookingReference, "50000.00", "USD", 2, "PAY_VAL_002");
        var handler = new PayHereWebhookCommandHandler(db, service);

        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("currency");
        booking.Status.Should().Be(BookingStatus.PendingPayment);
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Webhook_DirectBookingValidation_AmountMismatch_RejectsConfirmation()
    {
        await using var db = CreateDbContext();
        var booking = new SmartHotel.Booking.Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-WEBHOOK-MISMATCH-AMT",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Guest",
            CustomerEmail = "guest@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            TotalAmount = 50000m,
            Currency = "LKR",
            Status = BookingStatus.PendingPayment
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        // Provider sends 100 instead of 50000
        var payload = CreateSignedPayload(booking.BookingReference, "100.00", "LKR", 2, "PAY_VAL_003");
        var handler = new PayHereWebhookCommandHandler(db, service);

        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("amount");
        booking.Status.Should().Be(BookingStatus.PendingPayment);
        db.OutboxMessages.Should().BeEmpty();
    }
}
