using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Bookings.Commands;

public record CheckInCommand(Guid BookingId) : IRequest<Result<BookingDto>>;

public class CheckInCommandHandler : IRequestHandler<CheckInCommand, Result<BookingDto>>
{
    private readonly IBookingDbContext _context;

    public CheckInCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BookingDto>> Handle(CheckInCommand command, CancellationToken ct)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == command.BookingId, ct);
        if (booking == null)
        {
            return Result<BookingDto>.Failure($"Booking with ID {command.BookingId} was not found.");
        }

        try
        {
            booking.CheckIn();
        }
        catch (InvalidOperationException ex)
        {
            return Result<BookingDto>.Failure(ex.Message);
        }

        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "booking.checkedin",
            Content = JsonSerializer.Serialize(new
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                CustomerId = booking.CustomerId,
                RoomId = booking.RoomId,
                CheckInDate = booking.CheckInDate,
                OccurredOnUtc = DateTime.UtcNow
            })
        });

        await _context.SaveChangesAsync(ct);

        return Result<BookingDto>.Success(new BookingDto
        {
            Id = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            CustomerLastName = booking.CustomerLastName,
            CustomerEmail = booking.CustomerEmail,
            RoomId = booking.RoomId,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            PaymentReference = booking.PaymentReference,
            PayHereOrderId = booking.PayHereOrderId,
            CreatedAtUtc = booking.CreatedAtUtc
        }, "Guest checked in successfully.");
    }
}

public record CheckOutCommand(Guid BookingId) : IRequest<Result<BookingDto>>;

public class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, Result<BookingDto>>
{
    private readonly IBookingDbContext _context;

    public CheckOutCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BookingDto>> Handle(CheckOutCommand command, CancellationToken ct)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == command.BookingId, ct);
        if (booking == null)
        {
            return Result<BookingDto>.Failure($"Booking with ID {command.BookingId} was not found.");
        }

        try
        {
            booking.CheckOut();
        }
        catch (InvalidOperationException ex)
        {
            return Result<BookingDto>.Failure(ex.Message);
        }

        // Outbox event: booking.checkedout (Consumed by Hotel Ops Service to transition room to Dirty)
        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "booking.checkedout",
            Content = JsonSerializer.Serialize(new
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                CustomerId = booking.CustomerId,
                RoomId = booking.RoomId,
                CheckOutDate = booking.CheckOutDate,
                OccurredOnUtc = DateTime.UtcNow
            })
        });

        await _context.SaveChangesAsync(ct);

        return Result<BookingDto>.Success(new BookingDto
        {
            Id = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            CustomerLastName = booking.CustomerLastName,
            CustomerEmail = booking.CustomerEmail,
            RoomId = booking.RoomId,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            PaymentReference = booking.PaymentReference,
            PayHereOrderId = booking.PayHereOrderId,
            CreatedAtUtc = booking.CreatedAtUtc
        }, "Guest checked out successfully.");
    }
}
