using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Booking.Application.Features.Payments.Commands;
using SmartHotel.Booking.Application.Features.Payments.DTOs;
using SmartHotel.Booking.Application.Features.Payments.Queries;
using SmartHotel.Booking.Application.Interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Authorization;

namespace SmartHotel.Booking.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly BookingDbContext _db;
    private readonly IAuthorizationService _authorization;

    public PaymentsController(IMediator mediator, BookingDbContext db, IAuthorizationService authorization)
    {
        _mediator = mediator;
        _db = db;
        _authorization = authorization;
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
        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BookingId, ct);
        if (booking is null) return NotFound();
        if (!IsOwner() && booking.CustomerId != UserId()) return Forbid();
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

    /// <summary>
    /// Get payment details for a booking (Admin / Staff).
    /// </summary>
    [HttpGet("booking/{bookingId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentByBookingId(Guid bookingId, CancellationToken ct)
    {
        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        if (booking is null) return NotFound();
        var financeOrFrontOffice = IsFinance() || (await _authorization.AuthorizeAsync(User, HotelPolicies.FrontOfficeOperations)).Succeeded;
        if (!IsOwner() && !financeOrFrontOffice && booking.CustomerId != UserId()) return Forbid();
        var result = await _mediator.Send(new GetPaymentByBookingIdQuery(bookingId), ct);
        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(result.Data);
    }

    /// <summary>
    /// Manually confirm payment for a booking (Admin / FrontDesk).
    /// </summary>
    [HttpPost("{bookingId:guid}/confirm")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmPaymentManual(Guid bookingId, [FromBody] ConfirmPaymentManualDto? dto, CancellationToken ct)
    {
        await Task.CompletedTask;
        return StatusCode(StatusCodes.Status410Gone, new { message = "Manual payment confirmation is disabled. A verified provider webhook is required." });
    }

    private Guid? UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
    private string Role() => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "";
    private bool IsOwner() => Role() == "Owner";
    private bool IsFinance() => string.Equals(User.FindFirstValue("departmentCode"), "FINANCE", StringComparison.OrdinalIgnoreCase);
}

public record ConfirmPaymentManualDto(string? PaymentReference, string? Notes);
