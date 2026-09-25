using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Application.Common;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Application.Features.Bookings.Commands;

// ── DTOs ───────────────────────────────────────────────────────────────────────

public record BookingDraftDto(
    Guid Id,
    Guid RoomId,
    string RoomName,
    Guid RoomTypeId,
    string RatePlanId,
    string RatePlanName,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int GuestCount,
    int RoomsCount,
    decimal PricePerNight,
    int Nights,
    decimal TaxesAndFees,
    decimal TotalAmount,
    bool IsUpgraded,
    Guid? OriginalRoomId,
    string? OriginalRoomName,
    string Status,
    string? Currency = null
);

public record ContactInfoDto(
    string FirstName,
    string LastName,
    string Mobile,
    string Email
);

public record AddressInfoDto(
    string AddressType,
    string Country,
    string AddressLine1,
    string City
);

public record LoyaltyInfoDto(
    string? Program,
    string? LoyaltyId
);

public record CheckoutResultDto(
    string BookingReference,
    Guid BookingId,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    string RoomName,
    string RatePlanName,
    string ConfirmationMessage
);

// ── 1. Create Draft Command ───────────────────────────────────────────────────

public record CreateDraftCommand(
    Guid? CustomerId,
    string? CustomerEmail,
    Guid RoomId,
    string RoomName,
    Guid RoomTypeId,
    string RatePlanId,
    string RatePlanName,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int GuestCount,
    int RoomsCount,
    decimal PricePerNight
) : IRequest<Result<BookingDraftDto>>;

public class CreateDraftCommandHandler : IRequestHandler<CreateDraftCommand, Result<BookingDraftDto>>
{
    private readonly IBookingDbContext _dbContext;
    private readonly IHotelOpsClient _hotelOpsClient;

    public CreateDraftCommandHandler(IBookingDbContext dbContext, IHotelOpsClient hotelOpsClient)
    {
        _dbContext = dbContext;
        _hotelOpsClient = hotelOpsClient;
    }

    public async Task<Result<BookingDraftDto>> Handle(CreateDraftCommand request, CancellationToken ct)
    {
        // 1. Validate stay dates
        if (request.CheckInDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Result<BookingDraftDto>.Failure("Check-in date cannot be in the past.");
        }

        if (request.CheckOutDate <= request.CheckInDate)
        {
            return Result<BookingDraftDto>.Failure("Check-out date must be strictly after check-in date.");
        }

        if ((request.CheckOutDate.DayNumber - request.CheckInDate.DayNumber) > 30)
        {
            return Result<BookingDraftDto>.Failure("Stay duration cannot exceed 30 nights.");
        }

        // 2. Validate physical room existence and room-type association
        if (request.RoomId == Guid.Empty || request.RoomTypeId == Guid.Empty)
        {
            return Result<BookingDraftDto>.Failure("Room ID and Room Type ID are required.");
        }

        var room = await _hotelOpsClient.GetRoomAsync(request.RoomId, ct);
        if (room == null)
        {
            return Result<BookingDraftDto>.Failure("Selected physical room does not exist.");
        }

        if (room.RoomTypeId != request.RoomTypeId)
        {
            return Result<BookingDraftDto>.Failure("Selected room does not belong to the selected room type.");
        }

        // 3. Validate room type publication and capacity
        var roomType = await _hotelOpsClient.GetRoomTypeAsync(request.RoomTypeId, ct);
        if (roomType == null || !roomType.IsActive || !roomType.IsPublished)
        {
            return Result<BookingDraftDto>.Failure("Selected room type is invalid or not available for public booking.");
        }

        if (request.GuestCount <= 0)
        {
            return Result<BookingDraftDto>.Failure("Guest count must be at least 1.");
        }

        if (request.GuestCount > roomType.Capacity)
        {
            return Result<BookingDraftDto>.Failure($"Guest count ({request.GuestCount}) exceeds maximum room capacity ({roomType.Capacity}).");
        }

        // 4. Validate physical room availability against confirmed/pending bookings
        var hasConflict = await _dbContext.Bookings.AnyAsync(b =>
            b.RoomId == request.RoomId &&
            b.Status != BookingStatus.Cancelled &&
            request.CheckInDate < b.CheckOutDate &&
            request.CheckOutDate > b.CheckInDate,
            ct);

        if (hasConflict)
        {
            return Result<BookingDraftDto>.Failure("Selected room is not available for the requested dates.");
        }

        // 5. Enforce server-authoritative rate and currency
        if (request.PricePerNight > 0 && Math.Abs(request.PricePerNight - roomType.PricePerNight) > 0.01m)
        {
            return Result<BookingDraftDto>.Failure("Supplied price does not match current room type rate.");
        }

        if (string.IsNullOrWhiteSpace(roomType.Currency) || roomType.Currency.Trim().Length != 3)
        {
            return Result<BookingDraftDto>.Failure("Authoritative room type currency is missing or invalid.");
        }
        var authoritativeCurrency = roomType.Currency.Trim().ToUpperInvariant();

        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId ?? Guid.Empty,
            CustomerEmail = request.CustomerEmail ?? string.Empty,
            RoomId = request.RoomId,
            RoomName = string.IsNullOrWhiteSpace(request.RoomName) ? roomType.Name : request.RoomName,
            RoomTypeId = request.RoomTypeId,
            RatePlanId = request.RatePlanId,
            RatePlanName = request.RatePlanName,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            GuestCount = request.GuestCount,
            RoomsCount = request.RoomsCount > 0 ? request.RoomsCount : 1,
            PricePerNight = roomType.PricePerNight,
            Currency = authoritativeCurrency,
            Status = "Draft"
        };

        draft.RecalculateTotals();

        _dbContext.BookingDrafts.Add(draft);
        await _dbContext.SaveChangesAsync(ct);

        return Result<BookingDraftDto>.Success(MapToDto(draft));
    }

    private static BookingDraftDto MapToDto(BookingDraft d) =>
        new(d.Id, d.RoomId, d.RoomName, d.RoomTypeId, d.RatePlanId, d.RatePlanName,
            d.CheckInDate, d.CheckOutDate, d.GuestCount, d.RoomsCount, d.PricePerNight,
            d.Nights, d.TaxesAndFees, d.TotalAmount, d.IsUpgraded, d.OriginalRoomId,
            d.OriginalRoomName, d.Status, d.Currency);
}

// ── 2. Apply Upgrade Command ──────────────────────────────────────────────────

public record ApplyUpgradeRequest(Guid NewRoomId, Guid NewRatePlanId, string? NewRoomName, string? NewRatePlanName);

public record ApplyUpgradeCommand(
    Guid DraftId,
    Guid NewRoomId,
    Guid NewRatePlanId,
    string? NewRoomName = null,
    string? NewRatePlanName = null
) : IRequest<Result<BookingDraftDto>>;

public class ApplyUpgradeCommandHandler : IRequestHandler<ApplyUpgradeCommand, Result<BookingDraftDto>>
{
    private readonly IBookingDbContext _dbContext;

    public ApplyUpgradeCommandHandler(IBookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<BookingDraftDto>> Handle(ApplyUpgradeCommand request, CancellationToken ct)
    {
        var draft = await _dbContext.BookingDrafts.FirstOrDefaultAsync(d => d.Id == request.DraftId, ct);
        if (draft == null)
        {
            return Result<BookingDraftDto>.Failure("Booking draft not found.");
        }

        // Server-authoritative room name and pricing resolution
        var upgradeName = !string.IsNullOrWhiteSpace(request.NewRoomName) 
            ? request.NewRoomName 
            : "Signature Slowhouse Suite";

        var upgradeRatePlan = !string.IsNullOrWhiteSpace(request.NewRatePlanName)
            ? request.NewRatePlanName
            : "Member Exclusive — All Inclusive & Geothermal Ritual";

        // Server-authoritative base price calculation based on upgrade tier
        decimal newPricePerNight = draft.PricePerNight + 115m;
        if (newPricePerNight < 430m) newPricePerNight = 430m;

        draft.ApplyUpgrade(
            request.NewRoomId,
            upgradeName,
            request.NewRatePlanId.ToString(),
            upgradeRatePlan,
            newPricePerNight
        );

        await _dbContext.SaveChangesAsync(ct);

        return Result<BookingDraftDto>.Success(new BookingDraftDto(
            draft.Id, draft.RoomId, draft.RoomName, draft.RoomTypeId,
            draft.RatePlanId, draft.RatePlanName, draft.CheckInDate,
            draft.CheckOutDate, draft.GuestCount, draft.RoomsCount,
            draft.PricePerNight, draft.Nights, draft.TaxesAndFees,
            draft.TotalAmount, draft.IsUpgraded, draft.OriginalRoomId,
            draft.OriginalRoomName, draft.Status, draft.Currency
        ));
    }
}

// ── 3. Checkout Command ───────────────────────────────────────────────────────

public record CheckoutRequest(
    Guid DraftId,
    ContactInfoDto Contact,
    AddressInfoDto? Address,
    string? SpecialRequests,
    LoyaltyInfoDto? Loyalty,
    string PaymentToken,
    string? CouponCode
);

public record CheckoutCommand(CheckoutRequest Request, Guid? CustomerId = null) : IRequest<Result<CheckoutResultDto>>;

public class CheckoutCommandHandler : IRequestHandler<CheckoutCommand, Result<CheckoutResultDto>>
{
    private readonly IBookingDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IHotelOpsClient _hotelOpsClient;

    public CheckoutCommandHandler(
        IBookingDbContext dbContext,
        IPaymentGateway paymentGateway,
        IHotelOpsClient hotelOpsClient)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _hotelOpsClient = hotelOpsClient;
    }

    public async Task<Result<CheckoutResultDto>> Handle(CheckoutCommand request, CancellationToken ct)
    {
        var payload = request.Request;

        // 1. Validation of contact details
        if (payload.Contact == null ||
            string.IsNullOrWhiteSpace(payload.Contact.FirstName) ||
            string.IsNullOrWhiteSpace(payload.Contact.LastName) ||
            string.IsNullOrWhiteSpace(payload.Contact.Email))
        {
            return Result<CheckoutResultDto>.Failure("First name, last name, and email are required.");
        }

        // 2. Fetch Booking Draft
        var draft = await _dbContext.BookingDrafts.FirstOrDefaultAsync(d => d.Id == payload.DraftId, ct);
        if (draft == null)
        {
            return Result<CheckoutResultDto>.Failure("Booking draft not found.");
        }

        // 3. Idempotency Check: if this draft was already completed, return existing confirmation
        if (draft.Status == "Completed")
        {
            var existing = await _dbContext.Bookings
                .Where(b => b.PaymentReference == $"DRAFT-{draft.Id}")
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                return Result<CheckoutResultDto>.Success(new CheckoutResultDto(
                    existing.BookingReference,
                    existing.Id,
                    existing.Status.ToString(),
                    existing.TotalAmount,
                    "USD",
                    existing.CheckInDate,
                    existing.CheckOutDate,
                    draft.RoomName,
                    draft.RatePlanName,
                    "Your reservation was already confirmed."
                ));
            }
        }

        if (draft.Status == "PendingPayment")
        {
            return Result<CheckoutResultDto>.Failure("Checkout is already in progress for this booking draft. Please wait a moment.");
        }

        if (draft.Status != "Draft" && draft.Status != "Upgraded")
        {
            return Result<CheckoutResultDto>.Failure($"Cannot checkout draft in status '{draft.Status}'.");
        }

        // 4. Enforce Draft Ownership
        if (draft.CustomerId != Guid.Empty)
        {
            if (request.CustomerId == null || request.CustomerId == Guid.Empty)
            {
                return Result<CheckoutResultDto>.Failure("Authentication required: this booking draft belongs to a registered guest.");
            }
            if (request.CustomerId.Value != draft.CustomerId)
            {
                return Result<CheckoutResultDto>.Failure("Unauthorized: this booking draft belongs to another guest.");
            }
        }

        var effectiveCustomerId = request.CustomerId ?? draft.CustomerId;

        // 5. Re-verify physical room existence and room-type association with Hotel Ops
        var room = await _hotelOpsClient.GetRoomAsync(draft.RoomId, ct);
        if (room == null)
        {
            return Result<CheckoutResultDto>.Failure("Selected physical room does not exist.");
        }

        if (room.RoomTypeId != draft.RoomTypeId)
        {
            return Result<CheckoutResultDto>.Failure("Selected room does not belong to the selected room type.");
        }

        var roomType = await _hotelOpsClient.GetRoomTypeAsync(draft.RoomTypeId, ct);
        if (roomType == null || !roomType.IsActive || !roomType.IsPublished)
        {
            return Result<CheckoutResultDto>.Failure("Selected room type is invalid or not available for public booking.");
        }

        // 6. Enforce trusted server-side price calculation
        var nights = Math.Max(1, draft.CheckOutDate.DayNumber - draft.CheckInDate.DayNumber);
        var subtotal = roomType.PricePerNight * nights * Math.Max(1, draft.RoomsCount);
        var expectedTaxes = Math.Round(subtotal * 0.10m, 2);
        var expectedTotal = subtotal + expectedTaxes;

        if (Math.Abs(draft.TotalAmount - expectedTotal) > 0.05m)
        {
            return Result<CheckoutResultDto>.Failure("Booking price does not match official room rates.");
        }

        // Validate authoritative draft currency
        if (string.IsNullOrWhiteSpace(draft.Currency) || draft.Currency.Trim().Length != 3)
        {
            return Result<CheckoutResultDto>.Failure("Booking draft has missing or invalid currency.");
        }
        var currency = draft.Currency.Trim().ToUpperInvariant();

        // 7. Phase 1: Fast Reservation & PostgreSQL Advisory Lock (< 5ms)
        var bookingReference = Domain.Entities.Booking.GenerateBookingReference();
        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = bookingReference,
            CustomerId = effectiveCustomerId,
            CustomerLastName = payload.Contact.LastName.Trim(),
            CustomerEmail = payload.Contact.Email.Trim().ToLowerInvariant(),
            RoomId = draft.RoomId,
            RoomNumber = room.RoomNumber, // Real physical room number from Hotel Ops!
            RoomTypeId = draft.RoomTypeId,
            CheckInDate = draft.CheckInDate,
            CheckOutDate = draft.CheckOutDate,
            GuestCount = draft.GuestCount,
            TotalAmount = expectedTotal,
            Currency = currency,
            Status = BookingStatus.PendingPayment,
            PaymentReference = $"DRAFT-{payload.DraftId}",
            CheckoutInitiatedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await using (var transaction = await _dbContext.BeginBookingTransactionAsync(ct))
        {
            using var lockScope = await _dbContext.AcquireRoomLockAsync(draft.RoomId, ct);

            var nowUtc = DateTime.UtcNow;

            // Check if this draft already has an active or completed reservation
            var existingDraftBooking = await _dbContext.Bookings.FirstOrDefaultAsync(b =>
                b.PaymentReference == $"DRAFT-{payload.DraftId}" &&
                b.Status != BookingStatus.Cancelled,
                ct);

            if (existingDraftBooking != null)
            {
                if (transaction is not null) await transaction.RollbackAsync(ct);
                if (existingDraftBooking.Status == BookingStatus.Confirmed)
                {
                    return Result<CheckoutResultDto>.Success(new CheckoutResultDto(
                        existingDraftBooking.BookingReference,
                        existingDraftBooking.Id,
                        existingDraftBooking.Status.ToString(),
                        existingDraftBooking.TotalAmount,
                        existingDraftBooking.Currency ?? currency,
                        existingDraftBooking.CheckInDate,
                        existingDraftBooking.CheckOutDate,
                        draft.RoomName,
                        draft.RatePlanName,
                        "Your reservation was already confirmed."
                    ));
                }
                return Result<CheckoutResultDto>.Failure("Checkout is already in progress for this booking draft. Please wait a moment.");
            }

            var hasConflict = await _dbContext.Bookings.AnyAsync(b =>
                b.RoomId == draft.RoomId &&
                b.Status != BookingStatus.Cancelled &&
                draft.CheckInDate < b.CheckOutDate &&
                draft.CheckOutDate > b.CheckInDate,
                ct);

            if (hasConflict)
            {
                if (transaction is not null) await transaction.RollbackAsync(ct);
                return Result<CheckoutResultDto>.Failure("The room is already booked for the selected dates. Please choose another date range or room.");
            }

            // Mark draft in-progress to block subsequent checkout requests
            draft.Status = "PendingPayment";
            draft.UpdatedAtUtc = DateTime.UtcNow;

            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }

        // 8. Phase 2: External Payment Execution (Outside DB Lock & Transaction)
        //    Classify the outcome strictly: only an authoritative provider decline
        //    is ConfirmedDecline. Timeouts and network errors are Unknown.
        PaymentProcessResult paymentResult;
        try
        {
            paymentResult = await _paymentGateway.ProcessTokenizedPaymentAsync(
                payload.PaymentToken,
                expectedTotal,
                currency,
                bookingReference,
                ct);
        }
        catch (TimeoutException ex)
        {
            paymentResult = new PaymentProcessResult(
                PaymentExecutionOutcome.Unknown,
                string.Empty,
                $"Payment gateway timed out: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            paymentResult = new PaymentProcessResult(
                PaymentExecutionOutcome.Unknown,
                string.Empty,
                $"Payment gateway request was cancelled: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            paymentResult = new PaymentProcessResult(
                PaymentExecutionOutcome.Unknown,
                string.Empty,
                $"Payment gateway network failure: {ex.Message}");
        }

        // 9. Phase 3: State Resolution based on classified outcome
        switch (paymentResult.Outcome)
        {
            case PaymentExecutionOutcome.ConfirmedDecline:
                // Provider gave an explicit, authoritative decline.
                // Safe to cancel the pending booking and reset the draft for retry.
                booking.Status = BookingStatus.Cancelled;
                booking.UpdatedAtUtc = DateTime.UtcNow;
                draft.Status = "Draft";
                draft.UpdatedAtUtc = DateTime.UtcNow;
                try
                {
                    await _dbContext.SaveChangesAsync(ct);
                }
                catch
                {
                    // Suppress secondary error during rollback; draft remains PendingPayment
                    // in-memory but the DB state may differ — reconciliation will clean up.
                }

                return Result<CheckoutResultDto>.Failure(
                    string.IsNullOrWhiteSpace(paymentResult.ErrorMessage)
                        ? "Your card was declined. Please verify your card details and try again."
                        : paymentResult.ErrorMessage);

            case PaymentExecutionOutcome.Unknown:
                // Outcome is ambiguous (timeout, network failure, HTTP 5xx, etc.).
                // DO NOT cancel the booking or reset the draft — the charge may have
                // already been collected. The PendingPayment status holds the room
                // and prevents it from being re-sold until reconciliation resolves it.
                return Result<CheckoutResultDto>.Failure(
                    $"Payment outcome could not be confirmed (possible network or gateway issue). " +
                    $"Your room is held temporarily. If payment was charged, your booking reference is {bookingReference}. " +
                    $"Please contact support with this reference if you do not receive a confirmation shortly.");

            case PaymentExecutionOutcome.Success:
            default:
                // Payment succeeded: confirm reservation with retry/recovery.
                booking.ConfirmPayment($"DRAFT-{draft.Id}", paymentResult.TransactionId);
                draft.Status = "Completed";
                draft.UpdatedAtUtc = DateTime.UtcNow;

                var saveSucceeded = false;
                for (var retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        if (retry > 0) await Task.Delay(50 * retry, ct);
                        await _dbContext.SaveChangesAsync(ct);
                        saveSucceeded = true;
                        break;
                    }
                    catch
                    {
                        if (retry == 2) break;
                    }
                }

                if (!saveSucceeded)
                {
                    // Payment was collected but DB save failed after retries.
                    // Booking remains in PendingPayment in the DB — do NOT release the room.
                    return Result<CheckoutResultDto>.Failure(
                        $"Payment authorized successfully (Transaction: {paymentResult.TransactionId}), " +
                        $"but reservation confirmation encountered a database save error. " +
                        $"Your booking reference is {bookingReference}. Please contact support with this reference.");
                }

                return Result<CheckoutResultDto>.Success(new CheckoutResultDto(
                    booking.BookingReference,
                    booking.Id,
                    "Confirmed",
                    booking.TotalAmount,
                    booking.Currency ?? currency,
                    booking.CheckInDate,
                    booking.CheckOutDate,
                    draft.RoomName,
                    draft.RatePlanName,
                    "Your stay at SmartHotel Maskeliya has been successfully reserved and confirmed."
                ));
        }
    }
}
