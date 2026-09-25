using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Bookings.Commands;

public record CreateBookingRequest
{
    public Guid CustomerId { get; init; }
    public string CustomerLastName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public Guid RoomId { get; init; }
    public Guid RoomTypeId { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public int GuestCount { get; init; }
}

public record CreateBookingCommand(CreateBookingRequest Request) : IRequest<Result<BookingDto>>;

public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, Result<BookingDto>>
{
    private readonly IBookingDbContext _context;
    private readonly IHotelOpsClient _hotelOpsClient;

    public CreateBookingCommandHandler(IBookingDbContext context, IHotelOpsClient hotelOpsClient)
    {
        _context = context;
        _hotelOpsClient = hotelOpsClient;
    }

    public async Task<Result<BookingDto>> Handle(CreateBookingCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Basic validation
        if (req.CheckOutDate <= req.CheckInDate)
        {
            return Result<BookingDto>.Failure("Check-out date must be strictly after check-in date.");
        }

        if (req.CheckInDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Result<BookingDto>.Failure("Check-in date cannot be in the past.");
        }

        if (req.GuestCount <= 0)
        {
            return Result<BookingDto>.Failure("Guest count must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(req.CustomerLastName) || string.IsNullOrWhiteSpace(req.CustomerEmail))
        {
            return Result<BookingDto>.Failure("Customer last name and email are required for booking.");
        }

        // 2. Synchronous Cross-Service Verification with Hotel Ops Service
        var roomType = await _hotelOpsClient.GetRoomTypeAsync(req.RoomTypeId, ct);
        if (roomType == null || !roomType.IsActive || !roomType.IsPublished)
        {
            return Result<BookingDto>.Failure("Selected room type is invalid or not available for public booking.");
        }

        if (req.GuestCount > roomType.Capacity)
        {
            return Result<BookingDto>.Failure($"Guest count ({req.GuestCount}) exceeds maximum room capacity ({roomType.Capacity}).");
        }

        var room = await _hotelOpsClient.GetRoomAsync(req.RoomId, ct);
        if (room == null)
        {
            return Result<BookingDto>.Failure("Selected room does not exist.");
        }

        // 3. Concurrency Protection & Anti-Double-Booking Check
        await using var transaction = await _context.BeginBookingTransactionAsync(ct);

        // The command owns the transaction; the xact lock is released on commit or rollback.
        using var lockScope = await _context.AcquireRoomLockAsync(req.RoomId, ct);

        var hasConflict = await _context.Bookings.AnyAsync(b =>
            b.RoomId == req.RoomId &&
            b.Status != BookingStatus.Cancelled &&
            req.CheckInDate < b.CheckOutDate &&
            req.CheckOutDate > b.CheckInDate,
            ct);

        if (hasConflict)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }
            return Result<BookingDto>.Failure("The room is already booked for the selected dates. Please choose another date range or room.");
        }

        // 4. Compute Total Amount and Currency
        if (string.IsNullOrWhiteSpace(roomType.Currency) || roomType.Currency.Trim().Length != 3)
        {
            return Result<BookingDto>.Failure("Authoritative room type currency is missing or invalid.");
        }
        var currency = roomType.Currency.Trim().ToUpperInvariant();

        var nights = req.CheckOutDate.DayNumber - req.CheckInDate.DayNumber;
        var totalAmount = (nights * roomType.PricePerNight) + roomType.CleaningFee + roomType.AmenitiesFee;

        // 5. Create Booking in PendingPayment state
        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = Domain.Entities.Booking.GenerateBookingReference(),
            CustomerId = req.CustomerId,
            CustomerLastName = req.CustomerLastName.Trim(),
            CustomerEmail = req.CustomerEmail.Trim(),
            RoomId = req.RoomId,
            RoomNumber = room.RoomNumber,
            RoomTypeId = req.RoomTypeId,
            CheckInDate = req.CheckInDate,
            CheckOutDate = req.CheckOutDate,
            GuestCount = req.GuestCount,
            TotalAmount = totalAmount,
            Currency = currency,
            Status = BookingStatus.PendingPayment
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }


        var dto = new BookingDto
        {
            Id = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            CustomerLastName = booking.CustomerLastName,
            CustomerEmail = booking.CustomerEmail,
            RoomId = booking.RoomId,
            RoomNumber = booking.RoomNumber,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Currency = booking.Currency,
            Status = booking.Status,
            CreatedAtUtc = booking.CreatedAtUtc
        };

        return Result<BookingDto>.Success(dto, "Booking created successfully in PendingPayment status.");
    }
}
