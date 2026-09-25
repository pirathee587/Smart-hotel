using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Kiosk.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Kiosk.Commands;

public record KioskCheckInCommand(string BookingReference, string LastName) : IRequest<Result<KioskBookingDto>>;

public class KioskCheckInCommandHandler : IRequestHandler<KioskCheckInCommand, Result<KioskBookingDto>>
{
    private readonly IBookingDbContext _context;
    private readonly IHotelOpsClient _hotelOps;

    public KioskCheckInCommandHandler(IBookingDbContext context,IHotelOpsClient hotelOps)
    {
        _context = context;
        _hotelOps=hotelOps;
    }

    public async Task<Result<KioskBookingDto>> Handle(KioskCheckInCommand command, CancellationToken ct)
    {
        var refNormalized = command.BookingReference.Trim().ToUpperInvariant();
        var nameNormalized = command.LastName.Trim().ToLowerInvariant();

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.BookingReference.ToUpper() == refNormalized &&
                                      b.CustomerLastName.ToLower() == nameNormalized, ct);

        if (booking == null)
        {
            return Result<KioskBookingDto>.Failure("No reservation found matching the provided reference and last name.");
        }

        if (booking.Status == BookingStatus.CheckedIn)
        {
            var checkedInDto = new KioskBookingDto
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                CustomerLastName = booking.CustomerLastName,
                CustomerEmail = booking.CustomerEmail,
                RoomId = booking.RoomId,
                RoomTypeId = booking.RoomTypeId,
                CheckInDate = booking.CheckInDate,
                CheckOutDate = booking.CheckOutDate,
                GuestCount = booking.GuestCount,
                TotalAmount = booking.TotalAmount,
                Status = booking.Status,
                CanCheckIn = false,
                CanCheckOut = true
            };
            return Result<KioskBookingDto>.Success(checkedInDto, "You are already checked in. Welcome to SmartHotel!");
        }

        if (booking.Status != BookingStatus.Confirmed)
            return Result<KioskBookingDto>.Failure($"Cannot check in. Current booking status is {booking.Status}. Only Confirmed reservations can check in.");
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today < booking.CheckInDate)
            return Result<KioskBookingDto>.Failure($"Early check-in not permitted at kiosk. Your check-in date is {booking.CheckInDate}. Please visit the front desk.");

        await using var transaction = await _context.BeginBookingTransactionAsync(ct);
        using var roomLock = await _context.AcquireRoomLockAsync(booking.RoomId, ct);
        booking = await _context.Bookings.FirstAsync(x => x.Id == booking.Id, ct);

        if (booking.Status == BookingStatus.CheckedIn)
        {
            var checkedInDto = new KioskBookingDto
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                CustomerLastName = booking.CustomerLastName,
                CustomerEmail = booking.CustomerEmail,
                RoomId = booking.RoomId,
                RoomTypeId = booking.RoomTypeId,
                CheckInDate = booking.CheckInDate,
                CheckOutDate = booking.CheckOutDate,
                GuestCount = booking.GuestCount,
                TotalAmount = booking.TotalAmount,
                Status = booking.Status,
                CanCheckIn = false,
                CanCheckOut = true
            };
            return Result<KioskBookingDto>.Success(checkedInDto, "You are already checked in. Welcome to SmartHotel!");
        }

        var blocker = await SmartHotel.Booking.Application.Features.Bookings.Commands.CheckInReadiness.Validate(_context, _hotelOps, booking, ct);
        if (blocker is not null) return Result<KioskBookingDto>.Failure(blocker);

        // Atomic Cross-Service Room Claim
        var claimResult = await _hotelOps.ClaimRoomAsync(booking.RoomId, booking.Id, "Kiosk", ct);
        if (!claimResult.Success || !claimResult.Claimed)
        {
            return Result<KioskBookingDto>.Failure(claimResult.Blocker ?? claimResult.Message ?? "Room claim failed; check-in blocked.");
        }

        try
        {
            booking.CheckIn();

            _context.FrontOfficeAuditLogs.Add(new FrontOfficeAuditLog
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                Action = "CheckIn",
                ActorRole = "Guest",
                Source = "Kiosk",
                PreviousState = BookingStatus.Confirmed.ToString(),
                NewState = BookingStatus.CheckedIn.ToString(),
                Details = $"Self-service kiosk check-in for {booking.CustomerLastName} into room {booking.RoomNumber} ({booking.RoomId})",
                TimestampUtc = DateTime.UtcNow
            });

            _context.OutboxMessages.Add(new OutboxMessage
            {
                Type = "booking.checkedin",
                Content = JsonSerializer.Serialize(new
                {
                    BookingId = booking.Id,
                    BookingReference = booking.BookingReference,
                    CustomerId = booking.CustomerId,
                    RoomId = booking.RoomId,
                    Source = "Kiosk",
                    OccurredOnUtc = DateTime.UtcNow
                })
            });

            await _context.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);

            var dto = new KioskBookingDto
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                CustomerLastName = booking.CustomerLastName,
                CustomerEmail = booking.CustomerEmail,
                RoomId = booking.RoomId,
                RoomTypeId = booking.RoomTypeId,
                CheckInDate = booking.CheckInDate,
                CheckOutDate = booking.CheckOutDate,
                GuestCount = booking.GuestCount,
                TotalAmount = booking.TotalAmount,
                Status = booking.Status,
                CanCheckIn = false,
                CanCheckOut = true
            };

            return Result<KioskBookingDto>.Success(dto, "Self-service check-in successful! Welcome to SmartHotel.");
        }
        catch (Exception ex)
        {
            await _hotelOps.ReleaseRoomClaimAsync(booking.RoomId, booking.Id, CancellationToken.None);
            return Result<KioskBookingDto>.Failure($"Check-in failed during commit: {ex.Message}");
        }
    }
}

public record KioskCheckOutCommand(string BookingReference, string LastName) : IRequest<Result<KioskBookingDto>>;

public class KioskCheckOutCommandHandler : IRequestHandler<KioskCheckOutCommand, Result<KioskBookingDto>>
{
    private readonly IBookingDbContext _context;

    public KioskCheckOutCommandHandler(IBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<KioskBookingDto>> Handle(KioskCheckOutCommand command, CancellationToken ct)
    {
        var refNormalized = command.BookingReference.Trim().ToUpperInvariant();
        var nameNormalized = command.LastName.Trim().ToLowerInvariant();

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.BookingReference.ToUpper() == refNormalized &&
                                      b.CustomerLastName.ToLower() == nameNormalized, ct);

        if (booking == null)
        {
            return Result<KioskBookingDto>.Failure("No reservation found matching the provided reference and last name.");
        }

        if (booking.Status != BookingStatus.CheckedIn)
        {
            return Result<KioskBookingDto>.Failure($"Cannot check out. Current booking status is {booking.Status}. Only CheckedIn reservations can check out.");
        }

        booking.CheckOut();

        _context.FrontOfficeAuditLogs.Add(new FrontOfficeAuditLog
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            Action = "CheckOut",
            ActorRole = "Guest",
            Source = "Kiosk",
            PreviousState = BookingStatus.CheckedIn.ToString(),
            NewState = BookingStatus.CheckedOut.ToString(),
            Details = $"Self-service kiosk check-out for {booking.CustomerLastName} from room {booking.RoomNumber} ({booking.RoomId})",
            TimestampUtc = DateTime.UtcNow
        });

        // Outbox event: booking.checkedout (signals Hotel Ops Service to set room status to Dirty)
        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "booking.checkedout",
            Content = JsonSerializer.Serialize(new
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                CustomerId = booking.CustomerId,
                RoomId = booking.RoomId,
                Source = "Kiosk",
                OccurredOnUtc = DateTime.UtcNow
            })
        });

        await _context.SaveChangesAsync(ct);

        var dto = new KioskBookingDto
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerLastName = booking.CustomerLastName,
            CustomerEmail = booking.CustomerEmail,
            RoomId = booking.RoomId,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            CanCheckIn = false,
            CanCheckOut = false
        };

        return Result<KioskBookingDto>.Success(dto, "Self-service check-out completed! We hope you enjoyed your stay.");
    }
}
