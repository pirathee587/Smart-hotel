using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.Booking.Application.Features.Payments.Commands;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Booking.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Booking.Tests;

public sealed class PayHereWebhookValidationTests
{
    private const string MerchantId = "test-merchant-id";
    private const string MerchantSecret = "test-secret-key";

    [Fact]
    public async Task PaymentExists_CorrectAmountAndCurrency_ExistingValidPathPreserved()
    {
        // Arrange
        await using var db = CreateContext();
        var booking = CreateBooking(150.00m, "LKR");
        var payment = CreatePayment(booking, 150.00m, "LKR");
        db.AddRange(booking, payment);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "150.00", "LKR", 2, "PH_PAY_001");
        var handler = new PayHereWebhookCommandHandler(db, service);

        // Act
        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        // Assert
        result.Succeeded.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Completed);
        booking.Status.Should().Be(BookingStatus.Confirmed);
        db.OutboxMessages.Should().Contain(m => m.Type == "booking.confirmed");
        db.OutboxMessages.Should().Contain(m => m.Type == "payment.completed");
    }

    [Fact]
    public async Task PaymentExists_AmountMismatch_Rejected()
    {
        // Arrange
        await using var db = CreateContext();
        var booking = CreateBooking(150.00m, "LKR");
        var payment = CreatePayment(booking, 150.00m, "LKR");
        db.AddRange(booking, payment);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "1.00", "LKR", 2, "PH_PAY_002");
        var handler = new PayHereWebhookCommandHandler(db, service);

        // Act
        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("amount");
        payment.Status.Should().Be(PaymentStatus.Created);
        booking.Status.Should().Be(BookingStatus.PendingPayment);
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task PaymentExists_CurrencyMismatch_Rejected()
    {
        // Arrange
        await using var db = CreateContext();
        var booking = CreateBooking(150.00m, "LKR");
        var payment = CreatePayment(booking, 150.00m, "LKR");
        db.AddRange(booking, payment);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "150.00", "USD", 2, "PH_PAY_003");
        var handler = new PayHereWebhookCommandHandler(db, service);

        // Act
        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("currency");
        payment.Status.Should().Be(PaymentStatus.Created);
        booking.Status.Should().Be(BookingStatus.PendingPayment);
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task PaymentAbsent_BookingExists_CorrectAuthoritativeAmountAndCurrency_AcceptedOnlyWhenEstablished()
    {
        // Arrange
        await using var db = CreateContext();
        var booking = CreateBooking(250.00m, "LKR");
        db.Add(booking);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "250.00", "LKR", 2, "PH_PAY_004");
        var handler = new PayHereWebhookCommandHandler(db, service);

        // Act
        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        // Assert
        result.Succeeded.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        db.OutboxMessages.Should().Contain(m => m.Type == "booking.confirmed");
        db.OutboxMessages.Should().Contain(m => m.Type == "payment.completed");
    }

    [Fact]
    public async Task PaymentAbsent_BookingExists_NoAuthoritativeCurrencyEstablished_RejectedSafely()
    {
        // Arrange: Booking has TotalAmount = 250m, but Booking entity does not persist Currency
        await using var db = CreateContext();
        var booking = CreateBooking(250.00m, currency: null);
        db.Add(booking);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "250.00", "LKR", 2, "PH_PAY_005");
        var handler = new PayHereWebhookCommandHandler(db, service);

        // Act
        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Authoritative booking currency cannot be established");
        booking.Status.Should().Be(BookingStatus.PendingPayment);
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task PaymentAbsent_BookingExists_AmountMismatch_Rejected()
    {
        // Arrange
        await using var db = CreateContext();
        var booking = CreateBooking(250.00m, "LKR");
        db.Add(booking);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "10.00", "LKR", 2, "PH_PAY_006");
        var handler = new PayHereWebhookCommandHandler(db, service);

        // Act
        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("amount");
        booking.Status.Should().Be(BookingStatus.PendingPayment);
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task PaymentAbsent_BookingExists_CurrencyMismatch_Rejected()
    {
        // Arrange
        await using var db = CreateContext();
        var booking = CreateBooking(250.00m, "LKR");
        db.Add(booking);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "250.00", "USD", 2, "PH_PAY_007");
        var handler = new PayHereWebhookCommandHandler(db, service);

        // Act
        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("currency");
        booking.Status.Should().Be(BookingStatus.PendingPayment);
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task MalformedAmount_Rejected()
    {
        // Arrange
        await using var db = CreateContext();
        var booking = CreateBooking(250.00m, "LKR");
        var payment = CreatePayment(booking, 250.00m, "LKR");
        db.AddRange(booking, payment);
        await db.SaveChangesAsync();

        var service = CreatePayHereService();
        var payload = CreateSignedPayload(booking.BookingReference, "not-a-number", "LKR", 2, "PH_PAY_008");
        var handler = new PayHereWebhookCommandHandler(db, service);

        // Act
        var result = await handler.Handle(new PayHereWebhookCommand(payload), default);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Invalid provider amount format");
        payment.Status.Should().Be(PaymentStatus.Created);
        booking.Status.Should().Be(BookingStatus.PendingPayment);
        db.OutboxMessages.Should().BeEmpty();
    }

    private static BookingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .Options;
        return new BookingDbContext(options);
    }

    private static SmartHotel.Booking.Domain.Entities.Booking CreateBooking(decimal totalAmount, string? currency = "LKR")
    {
        return new SmartHotel.Booking.Domain.Entities.Booking
        {
            BookingReference = $"TH-TEST-{Guid.NewGuid():N}",
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "Guest",
            CustomerEmail = "guest@example.com",
            RoomId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            TotalAmount = totalAmount,
            Currency = currency,
            Status = BookingStatus.PendingPayment
        };
    }

    private static Payment CreatePayment(SmartHotel.Booking.Domain.Entities.Booking booking, decimal amount, string currency)
    {
        return new Payment
        {
            BookingId = booking.Id,
            Booking = booking,
            Amount = amount,
            Currency = currency,
            Provider = PaymentProvider.PayHere,
            PayHereOrderId = booking.BookingReference,
            Status = PaymentStatus.Created
        };
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
}
