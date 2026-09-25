using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Bookings.Commands;

public record CheckInCommand(Guid BookingId, Guid? ActorUserId = null, string Source = "FrontOffice") : IRequest<Result<BookingDto>>;

public class CheckInCommandHandler : IRequestHandler<CheckInCommand, Result<BookingDto>>
{
    private readonly IBookingDbContext _context;
    private readonly IHotelOpsClient _hotelOps;

    public CheckInCommandHandler(IBookingDbContext context, IHotelOpsClient hotelOps)
    {
        _context = context;
        _hotelOps = hotelOps;
    }

    public async Task<Result<BookingDto>> Handle(CheckInCommand command, CancellationToken ct)
    {
        await using var transaction = await _context.BeginBookingTransactionAsync(ct);
        var initial = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == command.BookingId, ct);
        if (initial == null) return Result<BookingDto>.Failure($"Booking with ID {command.BookingId} was not found.");
        
        using var roomLock = await _context.AcquireRoomLockAsync(initial.RoomId, ct);
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == command.BookingId, ct);
        if (booking == null)
        {
            return Result<BookingDto>.Failure($"Booking with ID {command.BookingId} was not found.");
        }

        // Idempotency: if already checked in, return success without duplicate side effects
        if (booking.Status == BookingStatus.CheckedIn)
        {
            return Result<BookingDto>.Success(MapToDto(booking), "Guest is already checked in.");
        }

        var blocker = await CheckInReadiness.Validate(_context, _hotelOps, booking, ct);
        if (blocker is not null) return Result<BookingDto>.Failure(blocker);

        // Atomic Cross-Service Room Claim
        var claimResult = await _hotelOps.ClaimRoomAsync(booking.RoomId, booking.Id, command.Source, ct);
        if (!claimResult.Success || !claimResult.Claimed)
        {
            return Result<BookingDto>.Failure(claimResult.Blocker ?? claimResult.Message ?? "Room claim failed; check-in blocked.");
        }

        try
        {
            booking.CheckIn();

            _context.FrontOfficeAuditLogs.Add(new FrontOfficeAuditLog
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                Action = "CheckIn",
                ActorUserId = command.ActorUserId,
                ActorRole = command.Source == "Kiosk" ? "Guest" : "Receptionist",
                Source = command.Source,
                PreviousState = BookingStatus.Confirmed.ToString(),
                NewState = BookingStatus.CheckedIn.ToString(),
                Details = $"Guest {booking.CustomerLastName} checked into room {booking.RoomNumber} ({booking.RoomId})",
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
                    CheckInDate = booking.CheckInDate,
                    Source = command.Source,
                    OccurredOnUtc = DateTime.UtcNow
                })
            });

            _context.OutboxMessages.Add(new OutboxMessage
            {
                Type = "frontoffice.checkin-audited",
                Content = JsonSerializer.Serialize(new
                {
                    BookingId = booking.Id,
                    booking.RoomId,
                    command.ActorUserId,
                    command.Source,
                    Action = "CheckIn",
                    OccurredOnUtc = DateTime.UtcNow
                })
            });

            await _context.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);

            return Result<BookingDto>.Success(MapToDto(booking), "Guest checked in successfully.");
        }
        catch (Exception ex)
        {
            // Compensating transaction to release room claim if commit failed
            await _hotelOps.ReleaseRoomClaimAsync(booking.RoomId, booking.Id, CancellationToken.None);
            return Result<BookingDto>.Failure($"Check-in failed during commit: {ex.Message}");
        }
    }

    private static BookingDto MapToDto(Domain.Entities.Booking booking) => new()
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
        PaymentReference = booking.PaymentReference,
        PayHereOrderId = booking.PayHereOrderId,
        CreatedAtUtc = booking.CreatedAtUtc
    };
}

internal static class CheckInReadiness
{
    public static async Task<string?> Validate(IBookingDbContext context, IHotelOpsClient hotelOps, Domain.Entities.Booking booking, CancellationToken ct)
    {
        if (booking.Status != BookingStatus.Confirmed)
            return $"Cannot check in booking with status {booking.Status}. Only Confirmed bookings can check in.";
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today < booking.CheckInDate || today > booking.CheckOutDate)
            return $"Reservation is not eligible for check-in on {today}. Valid stay dates are {booking.CheckInDate} to {booking.CheckOutDate}.";
        
        if (await context.Bookings.AsNoTracking().AnyAsync(x => x.Id != booking.Id && x.RoomId == booking.RoomId && x.Status == BookingStatus.CheckedIn, ct))
            return "Room has a conflicting active occupancy.";
        
        var readiness = await hotelOps.GetRoomReadinessAsync(booking.RoomId, ct);
        if (readiness is null)
            return "Hotel Ops readiness is unavailable or unknown; check-in is blocked.";
        
        if (readiness.RoomId != booking.RoomId)
            return "Hotel Ops readiness response does not match the reservation room.";
        
        if (!readiness.ReadyForCheckIn)
            return readiness.Blocker ?? $"Room is not ready for check-in ({readiness.RoomStatus}/{readiness.ReadinessStatus}).";
        
        return null;
    }
}

public record CheckOutCommand(Guid BookingId, Guid? ActorUserId = null) : IRequest<Result<BookingDto>>;

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

        _context.FrontOfficeAuditLogs.Add(new FrontOfficeAuditLog
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            Action = "CheckOut",
            ActorUserId = command.ActorUserId,
            ActorRole = "Receptionist",
            Source = "FrontOffice",
            PreviousState = BookingStatus.CheckedIn.ToString(),
            NewState = BookingStatus.CheckedOut.ToString(),
            Details = $"Guest {booking.CustomerLastName} checked out of room {booking.RoomNumber} ({booking.RoomId})",
            TimestampUtc = DateTime.UtcNow
        });

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
            RoomNumber = booking.RoomNumber,
            RoomTypeId = booking.RoomTypeId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            GuestCount = booking.GuestCount,
            TotalAmount = booking.TotalAmount,
            Currency = booking.Currency,
            Status = booking.Status,
            PaymentReference = booking.PaymentReference,
            PayHereOrderId = booking.PayHereOrderId,
            CreatedAtUtc = booking.CreatedAtUtc
        }, "Guest checked out successfully.");
    }
}

