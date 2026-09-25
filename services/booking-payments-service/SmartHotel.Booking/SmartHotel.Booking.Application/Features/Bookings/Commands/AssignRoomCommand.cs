using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Bookings.Commands;

public record AssignRoomCommand(
    Guid BookingId,
    Guid NewRoomId,
    Guid? ActorUserId = null,
    string? Reason = null) : IRequest<Result<BookingDto>>;

public class AssignRoomCommandHandler : IRequestHandler<AssignRoomCommand, Result<BookingDto>>
{
    private readonly IBookingDbContext _context;
    private readonly IHotelOpsClient _hotelOps;

    public AssignRoomCommandHandler(IBookingDbContext context, IHotelOpsClient hotelOps)
    {
        _context = context;
        _hotelOps = hotelOps;
    }

    public async Task<Result<BookingDto>> Handle(AssignRoomCommand command, CancellationToken ct)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == command.BookingId, ct);
        if (booking == null)
        {
            return Result<BookingDto>.Failure($"Booking with ID {command.BookingId} was not found.");
        }

        if (booking.Status is BookingStatus.CheckedIn or BookingStatus.CheckedOut or BookingStatus.Cancelled)
        {
            return Result<BookingDto>.Failure($"Cannot reassign room for reservation with status {booking.Status}. Only Pending or Confirmed reservations can be assigned.");
        }

        // Validate target room exists in Hotel Ops
        var targetRoom = await _hotelOps.GetRoomAsync(command.NewRoomId, ct);
        if (targetRoom == null)
        {
            return Result<BookingDto>.Failure($"Target room with ID {command.NewRoomId} was not found in Hotel Operations.");
        }

        if (targetRoom.RoomTypeId != booking.RoomTypeId)
        {
            return Result<BookingDto>.Failure($"Target room {targetRoom.RoomNumber} belongs to a different room type. Mismatched room type assignment is not permitted.");
        }

        // Validate target room is not under Maintenance restriction or OutOfOrder
        var readiness = await _hotelOps.GetRoomReadinessAsync(command.NewRoomId, ct);
        if (readiness != null)
        {
            if (readiness.MaintenanceBlocked)
            {
                return Result<BookingDto>.Failure($"Room {targetRoom.RoomNumber} has an active Maintenance restriction ({readiness.Blocker}) and cannot be assigned.");
            }
            if (string.Equals(readiness.RoomStatus, "OutOfOrder", StringComparison.OrdinalIgnoreCase))
            {
                return Result<BookingDto>.Failure($"Room {targetRoom.RoomNumber} is currently Out of Order and cannot be assigned.");
            }
        }

        // Validate no overlapping reservations on target room
        var overlappingBooking = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(b =>
            b.Id != booking.Id &&
            b.RoomId == command.NewRoomId &&
            b.Status != BookingStatus.Cancelled &&
            b.CheckInDate < booking.CheckOutDate &&
            b.CheckOutDate > booking.CheckInDate, ct);

        if (overlappingBooking != null)
        {
            return Result<BookingDto>.Failure(
                $"Room {targetRoom.RoomNumber} is already reserved by booking {overlappingBooking.BookingReference} for overlapping dates ({overlappingBooking.CheckInDate} to {overlappingBooking.CheckOutDate}).");
        }

        await using var transaction = await _context.BeginBookingTransactionAsync(ct);
        using var roomLock = await _context.AcquireRoomLockAsync(command.NewRoomId, ct);

        var doubleCheckOverlap = await _context.Bookings.AsNoTracking().AnyAsync(b =>
            b.Id != booking.Id &&
            b.RoomId == command.NewRoomId &&
            b.Status != BookingStatus.Cancelled &&
            b.CheckInDate < booking.CheckOutDate &&
            b.CheckOutDate > booking.CheckInDate, ct);

        if (doubleCheckOverlap)
        {
            return Result<BookingDto>.Failure($"Room {targetRoom.RoomNumber} was reserved by another guest concurrently.");
        }

        var oldRoomId = booking.RoomId;
        var oldRoomNumber = booking.RoomNumber;

        booking.RoomId = targetRoom.Id;
        booking.RoomNumber = targetRoom.RoomNumber;
        booking.UpdatedAtUtc = DateTime.UtcNow;

        _context.FrontOfficeAuditLogs.Add(new FrontOfficeAuditLog
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            Action = "RoomAssigned",
            ActorUserId = command.ActorUserId,
            ActorRole = "FrontOffice",
            Source = "FrontOffice",
            Reason = command.Reason,
            PreviousState = $"Room {oldRoomNumber} ({oldRoomId})",
            NewState = $"Room {targetRoom.RoomNumber} ({targetRoom.Id})",
            Details = $"Room reassigned from {oldRoomNumber} to {targetRoom.RoomNumber}. Reason: {command.Reason ?? "Front Office reassignment"}",
            TimestampUtc = DateTime.UtcNow
        });

        _context.OutboxMessages.Add(new OutboxMessage
        {
            Type = "frontoffice.room-assigned",
            Content = JsonSerializer.Serialize(new
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                OldRoomId = oldRoomId,
                NewRoomId = targetRoom.Id,
                RoomNumber = targetRoom.RoomNumber,
                command.ActorUserId,
                OccurredOnUtc = DateTime.UtcNow
            })
        });

        await _context.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);

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
        }, $"Room {targetRoom.RoomNumber} successfully assigned to booking {booking.BookingReference}.");
    }
}
