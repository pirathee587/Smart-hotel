using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Payments.Commands;

public record CreatePayHereOrderRequest
{
    public Guid BookingId { get; init; }
    public string CustomerFirstName { get; init; } = "Guest";
    public string CustomerPhone { get; init; } = "+94771234567";
    public string? ReturnUrl { get; init; }
    public string? CancelUrl { get; init; }
    public string? NotifyUrl { get; init; }
}

public record CreatePayHereOrderCommand(CreatePayHereOrderRequest Request) : IRequest<Result<PayHereOrderResponse>>;

public class CreatePayHereOrderCommandHandler : IRequestHandler<CreatePayHereOrderCommand, Result<PayHereOrderResponse>>
{
    private readonly IBookingDbContext _context;
    private readonly IPayHereService _payHereService;

    public CreatePayHereOrderCommandHandler(IBookingDbContext context, IPayHereService payHereService)
    {
        _context = context;
        _payHereService = payHereService;
    }

    public async Task<Result<PayHereOrderResponse>> Handle(CreatePayHereOrderCommand command, CancellationToken ct)
    {
        var req = command.Request;

        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == req.BookingId, ct);
        if (booking == null)
        {
            return Result<PayHereOrderResponse>.Failure($"Booking with ID {req.BookingId} was not found.");
        }

        if (booking.Status != BookingStatus.PendingPayment)
        {
            return Result<PayHereOrderResponse>.Failure($"Cannot generate payment for booking in {booking.Status} state.");
        }

        var orderId = booking.BookingReference;
        var payHereOrderReq = new PayHereOrderRequest(
            OrderId: orderId,
            Amount: booking.TotalAmount,
            Currency: "LKR",
            CustomerFirstName: req.CustomerFirstName,
            CustomerLastName: booking.CustomerLastName,
            CustomerEmail: booking.CustomerEmail,
            CustomerPhone: req.CustomerPhone,
            ItemsDescription: $"SmartHotel Room Reservation {booking.BookingReference}",
            ReturnUrl: req.ReturnUrl ?? "https://smarthotel.lk/booking/success",
            CancelUrl: req.CancelUrl ?? "https://smarthotel.lk/booking/cancelled",
            NotifyUrl: req.NotifyUrl ?? "http://gateway:5000/api/v1/payments/payhere/notify");

        var payHereResponse = _payHereService.GenerateOrderRequest(payHereOrderReq);

        // Record initial payment entry
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = booking.TotalAmount,
            Currency = "LKR",
            Provider = PaymentProvider.PayHere,
            PayHereOrderId = orderId,
            Status = PaymentStatus.Created
        };

        booking.PayHereOrderId = orderId;
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(ct);

        return Result<PayHereOrderResponse>.Success(payHereResponse, "PayHere order generated successfully.");
    }
}
