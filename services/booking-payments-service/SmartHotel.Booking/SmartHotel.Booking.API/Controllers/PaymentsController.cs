using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Booking.Application.Features.Payments.Commands;
using SmartHotel.Booking.Application.Interfaces;

namespace SmartHotel.Booking.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Generate a signed PayHere checkout order for a pending reservation.
    /// </summary>
    [HttpPost("payhere/order")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PayHereOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePayHereOrder([FromBody] CreatePayHereOrderRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePayHereOrderCommand(request), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// PayHere payment notification webhook (IPN).
    /// Accepts both application/x-www-form-urlencoded and application/json.
    /// </summary>
    [HttpPost("payhere/notify")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded", "application/json")]
    public async Task<IActionResult> PayHereNotify(CancellationToken ct)
    {
        string merchantId = string.Empty;
        string orderId = string.Empty;
        string paymentId = string.Empty;
        string amount = string.Empty;
        string currency = string.Empty;
        int statusCode = 0;
        string md5sig = string.Empty;
        string? statusMessage = null;
        string? method = null;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            merchantId = form["merchant_id"].ToString();
            orderId = form["order_id"].ToString();
            paymentId = form["payment_id"].ToString();
            amount = form["payhere_amount"].ToString();
            currency = form["payhere_currency"].ToString();
            _ = int.TryParse(form["status_code"], out statusCode);
            md5sig = form["md5sig"].ToString();
            statusMessage = form["status_message"].ToString();
            method = form["method"].ToString();
        }
        else
        {
            try
            {
                var payload = await Request.ReadFromJsonAsync<PayHereWebhookPayload>(cancellationToken: ct);
                if (payload != null)
                {
                    merchantId = payload.MerchantId;
                    orderId = payload.OrderId;
                    paymentId = payload.PaymentId;
                    amount = payload.PayHereAmount;
                    currency = payload.PayHereCurrency;
                    statusCode = payload.StatusCode;
                    md5sig = payload.Md5Sig;
                    statusMessage = payload.StatusMessage;
                    method = payload.Method;
                }
            }
            catch
            {
                return BadRequest("Invalid JSON payload.");
            }
        }

        var webhookPayload = new PayHereWebhookPayload(
            MerchantId: merchantId,
            OrderId: orderId,
            PaymentId: paymentId,
            PayHereAmount: amount,
            PayHereCurrency: currency,
            StatusCode: statusCode,
            Md5Sig: md5sig,
            StatusMessage: statusMessage,
            Method: method);

        var result = await _mediator.Send(new PayHereWebhookCommand(webhookPayload), ct);
        if (!result.Succeeded)
        {
            return BadRequest(result.Message);
        }

        // PayHere expects 200 OK
        return Ok(new { status = "OK", message = result.Message });
    }
}
