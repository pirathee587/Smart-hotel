using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartHotel.Booking.Application.Interfaces;

namespace SmartHotel.Booking.Infrastructure.Services;

public class PayHereService : IPayHereService
{
    private readonly string _merchantId;
    private readonly string _merchantSecret;
    private readonly string _checkoutUrl;
    private readonly ILogger<PayHereService> _logger;

    public PayHereService(IConfiguration configuration, ILogger<PayHereService> logger)
    {
        _logger = logger;
        _merchantId = configuration["PAYHERE_MERCHANT_ID"] ?? configuration["PayHere:MerchantId"] ?? "1220001";
        _merchantSecret = configuration["PAYHERE_MERCHANT_SECRET"] ?? configuration["PayHere:MerchantSecret"] ?? "smarthotel_secret_sandbox_key_2026";
        var isSandbox = bool.TryParse(configuration["PAYHERE_IS_SANDBOX"] ?? configuration["PayHere:IsSandbox"], out var sandbox) ? sandbox : true;
        _checkoutUrl = isSandbox ? "https://sandbox.payhere.lk/pay/checkout" : "https://www.payhere.lk/pay/checkout";
    }

    public PayHereOrderResponse GenerateOrderRequest(PayHereOrderRequest request)
    {
        var formattedAmount = request.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var secretHash = ComputeMd5(_merchantSecret);
        var rawString = $"{_merchantId}{request.OrderId}{formattedAmount}{request.Currency}{secretHash}";
        var hash = ComputeMd5(rawString);

        var formFields = new Dictionary<string, string>
        {
            ["merchant_id"] = _merchantId,
            ["return_url"] = request.ReturnUrl,
            ["cancel_url"] = request.CancelUrl,
            ["notify_url"] = request.NotifyUrl,
            ["order_id"] = request.OrderId,
            ["items"] = request.ItemsDescription,
            ["currency"] = request.Currency,
            ["amount"] = formattedAmount,
            ["first_name"] = request.CustomerFirstName,
            ["last_name"] = request.CustomerLastName,
            ["email"] = request.CustomerEmail,
            ["phone"] = request.CustomerPhone,
            ["hash"] = hash
        };

        return new PayHereOrderResponse(
            MerchantId: _merchantId,
            OrderId: request.OrderId,
            Amount: request.Amount,
            Currency: request.Currency,
            Hash: hash,
            CheckoutUrl: _checkoutUrl,
            FormFields: formFields);
    }

    public bool VerifyWebhookSignature(PayHereWebhookPayload payload)
    {
        // PayHere Webhook verification formula:
        // md5sig = strtoupper(md5(merchant_id + order_id + payhere_amount + payhere_currency + status_code + strtoupper(md5(merchant_secret))))
        var secretHash = ComputeMd5(_merchantSecret);
        var rawString = $"{payload.MerchantId}{payload.OrderId}{payload.PayHereAmount}{payload.PayHereCurrency}{payload.StatusCode}{secretHash}";
        var calculatedSig = ComputeMd5(rawString);

        var isValid = string.Equals(calculatedSig, payload.Md5Sig, StringComparison.OrdinalIgnoreCase);
        if (!isValid)
        {
            _logger.LogWarning("PayHere signature mismatch! Received: {Received}, Calculated: {Calculated} for Order: {Order}",
                payload.Md5Sig, calculatedSig, payload.OrderId);
        }

        return isValid;
    }

    public static string ComputeMd5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes); // default is uppercase in .NET 8
    }
}
