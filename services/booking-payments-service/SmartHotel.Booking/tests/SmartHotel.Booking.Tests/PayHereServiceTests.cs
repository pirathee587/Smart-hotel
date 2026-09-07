using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class PayHereServiceTests
{
    private const string MerchantId = "1220001";
    private const string MerchantSecret = "smarthotel_secret_sandbox_key_2026";

    private readonly PayHereService _payHereService;

    public PayHereServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PAYHERE_MERCHANT_ID"] = MerchantId,
                ["PAYHERE_MERCHANT_SECRET"] = MerchantSecret,
                ["PAYHERE_IS_SANDBOX"] = "true"
            })
            .Build();

        _payHereService = new PayHereService(config, NullLogger<PayHereService>.Instance);
    }

    [Fact]
    public void GenerateOrderRequest_ComputesCorrectPayHereMd5Hash()
    {
        var request = new PayHereOrderRequest(
            OrderId: "TH-2026-TEST01",
            Amount: 25000.00m,
            Currency: "LKR",
            CustomerFirstName: "Kasun",
            CustomerLastName: "Silva",
            CustomerEmail: "kasun@example.com",
            CustomerPhone: "+94771234567",
            ItemsDescription: "Deluxe Suite Reservation",
            ReturnUrl: "https://smarthotel.lk/return",
            CancelUrl: "https://smarthotel.lk/cancel",
            NotifyUrl: "https://smarthotel.lk/notify");

        var response = _payHereService.GenerateOrderRequest(request);

        // Calculate expected hash independently
        var hashedSecret = PayHereService.ComputeMd5(MerchantSecret);
        var expectedRaw = $"{MerchantId}TH-2026-TEST0125000.00LKR{hashedSecret}";
        var expectedHash = PayHereService.ComputeMd5(expectedRaw);

        response.Hash.Should().Be(expectedHash);
        response.MerchantId.Should().Be(MerchantId);
        response.OrderId.Should().Be("TH-2026-TEST01");
        response.Amount.Should().Be(25000.00m);
        response.FormFields["hash"].Should().Be(expectedHash);
        response.FormFields["first_name"].Should().Be("Kasun");
        response.FormFields["last_name"].Should().Be("Silva");
        response.CheckoutUrl.Should().Contain("sandbox.payhere.lk");
    }

    [Fact]
    public void VerifyWebhookSignature_ValidSignature_ReturnsTrue()
    {
        var orderId = "TH-2026-TEST01";
        var amount = "25000.00";
        var currency = "LKR";
        var statusCode = 2; // Success

        var hashedSecret = PayHereService.ComputeMd5(MerchantSecret);
        var rawString = $"{MerchantId}{orderId}{amount}{currency}{statusCode}{hashedSecret}";
        var validMd5Sig = PayHereService.ComputeMd5(rawString);

        var payload = new PayHereWebhookPayload(
            MerchantId: MerchantId,
            OrderId: orderId,
            PaymentId: "PAYHERE_320000001",
            PayHereAmount: amount,
            PayHereCurrency: currency,
            StatusCode: statusCode,
            Md5Sig: validMd5Sig);

        var isValid = _payHereService.VerifyWebhookSignature(payload);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyWebhookSignature_TamperedAmount_ReturnsFalse()
    {
        var orderId = "TH-2026-TEST01";
        var amount = "25000.00";
        var currency = "LKR";
        var statusCode = 2;

        var hashedSecret = PayHereService.ComputeMd5(MerchantSecret);
        var rawString = $"{MerchantId}{orderId}{amount}{currency}{statusCode}{hashedSecret}";
        var validMd5Sig = PayHereService.ComputeMd5(rawString);

        // Attacker alters amount in payload but sends original sig
        var tamperedPayload = new PayHereWebhookPayload(
            MerchantId: MerchantId,
            OrderId: orderId,
            PaymentId: "PAYHERE_320000001",
            PayHereAmount: "100.00", // Tampered amount!
            PayHereCurrency: currency,
            StatusCode: statusCode,
            Md5Sig: validMd5Sig);

        var isValid = _payHereService.VerifyWebhookSignature(tamperedPayload);

        isValid.Should().BeFalse();
    }
}
