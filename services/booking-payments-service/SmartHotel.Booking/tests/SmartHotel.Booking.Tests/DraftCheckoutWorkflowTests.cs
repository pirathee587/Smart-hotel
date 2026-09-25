using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class DraftCheckoutWorkflowTests
{
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly Guid _roomTypeId = Guid.NewGuid();
    private readonly Guid _guestAId = Guid.NewGuid();
    private readonly Guid _guestBId = Guid.NewGuid();

    private BookingDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    private FakeHotelOpsClient CreateFakeHotelOpsClient(string roomNumber = "204", decimal pricePerNight = 250m, int capacity = 2)
    {
        return new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(
                Id: _roomTypeId,
                Name: "Deluxe Ocean Suite",
                PricePerNight: pricePerNight,
                CleaningFee: 25m,
                AmenitiesFee: 15m,
                Capacity: capacity,
                IsPublished: true,
                IsActive: true,
                Currency: "LKR"),
            Room = new RoomInfo(
                Id: _roomId,
                RoomTypeId: _roomTypeId,
                RoomNumber: roomNumber,
                Floor: 2,
                Status: "Available")
        };
    }

    private class TestPaymentGateway : IPaymentGateway
    {
        public PaymentExecutionOutcome Outcome { get; set; } = PaymentExecutionOutcome.Success;
        public string? ErrorMessage { get; set; }
        public string TransactionId { get; set; } = $"TXN-{Guid.NewGuid():N}";
        public int ProcessCount { get; private set; }
        public int DelayMs { get; set; } = 0;
        /// <summary>When set, the gateway throws this exception instead of returning a result.</summary>
        public Exception? ThrowException { get; set; }

        public async Task<PaymentProcessResult> ProcessTokenizedPaymentAsync(
            string paymentToken,
            decimal amount,
            string currency,
            string bookingReference,
            CancellationToken ct = default)
        {
            ProcessCount++;
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, ct);
            }
            if (ThrowException is not null) throw ThrowException;
            return new PaymentProcessResult(Outcome, TransactionId, ErrorMessage ?? string.Empty);
        }
    }

    [Fact]
    public async Task Checkout_Successful_CreatesConfirmedBookingWithPhysicalRoomNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(roomNumber: "204", pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway { Outcome = PaymentExecutionOutcome.Success };

        // 1. Create a legitimate draft
        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(13)); // 3 nights

        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId,
            CustomerEmail: "guestA@example.com",
            RoomId: _roomId,
            RoomName: "Deluxe Ocean Suite",
            RoomTypeId: _roomTypeId,
            RatePlanId: "STANDARD",
            RatePlanName: "Best Available Rate",
            CheckInDate: checkIn,
            CheckOutDate: checkOut,
            GuestCount: 2,
            RoomsCount: 1,
            PricePerNight: 200m
        ), CancellationToken.None);

        draftResult.Succeeded.Should().BeTrue();
        var draftId = draftResult.Data!.Id;

        // 2. Perform checkout
        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutRequest = new CheckoutRequest(
            DraftId: draftId,
            Contact: new ContactInfoDto("Eleanor", "Vance", "+15550199", "guestA@example.com"),
            Address: new AddressInfoDto("Home", "US", "123 Ocean Blvd", "Carmel"),
            SpecialRequests: "High floor",
            Loyalty: null,
            PaymentToken: "tok_visa_valid",
            CouponCode: null
        );

        var checkoutResult = await checkoutHandler.Handle(
            new CheckoutCommand(checkoutRequest, CustomerId: _guestAId),
            CancellationToken.None);

        // 3. Verify confirmation and physical room number
        checkoutResult.Succeeded.Should().BeTrue();
        checkoutResult.Data!.Status.Should().Be("Confirmed");
        checkoutResult.Data.BookingReference.Should().StartWith("TH-");

        // Verify booking in database
        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.Id == checkoutResult.Data.BookingId);
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.Confirmed);
        booking.RoomNumber.Should().Be("204", "Room number must come from Hotel Ops physical room, never hardcoded '101'");
        booking.RoomId.Should().Be(_roomId);
        booking.RoomTypeId.Should().Be(_roomTypeId);
        booking.CustomerId.Should().Be(_guestAId);
        booking.PaymentReference.Should().Be($"DRAFT-{draftId}");

        // Verify draft marked Completed
        var updatedDraft = await context.BookingDrafts.FindAsync(draftId);
        updatedDraft!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task CreateDraft_InvalidRoomId_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient();
        fakeClient.Room = null; // Room does not exist

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var result = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId,
            CustomerEmail: "guestA@example.com",
            RoomId: Guid.NewGuid(),
            RoomName: "Ocean Suite",
            RoomTypeId: _roomTypeId,
            RatePlanId: "STANDARD",
            RatePlanName: "Best Available Rate",
            CheckInDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            GuestCount: 2,
            RoomsCount: 1,
            PricePerNight: 250m
        ), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Selected physical room does not exist");
    }

    [Fact]
    public async Task CreateDraft_RoomTypeMismatch_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var differentRoomTypeId = Guid.NewGuid();
        var fakeClient = CreateFakeHotelOpsClient();
        fakeClient.Room = new RoomInfo(_roomId, _roomTypeId, "204", 2, "Available");

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var result = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId,
            CustomerEmail: "guestA@example.com",
            RoomId: _roomId,
            RoomName: "Ocean Suite",
            RoomTypeId: differentRoomTypeId,
            RatePlanId: "STANDARD",
            RatePlanName: "Best Available Rate",
            CheckInDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            GuestCount: 2,
            RoomsCount: 1,
            PricePerNight: 250m
        ), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Selected room does not belong to the selected room type");
    }

    [Fact]
    public async Task CreateDraft_ManipulatedPrice_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 250m); // Real rate is $250

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var result = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId,
            CustomerEmail: "guestA@example.com",
            RoomId: _roomId,
            RoomName: "Ocean Suite",
            RoomTypeId: _roomTypeId,
            RatePlanId: "STANDARD",
            RatePlanName: "Best Available Rate",
            CheckInDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            GuestCount: 2,
            RoomsCount: 1,
            PricePerNight: 10m // Tampered price: $10 instead of $250
        ), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Supplied price does not match current room type rate");
    }

    [Fact]
    public async Task Checkout_ManipulatedDraftPrice_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 250m);
        var paymentGateway = new TestPaymentGateway();

        // Seed a draft directly into DB that was tampered to have an illegitimate low total amount
        var tamperedDraft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestAId,
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STANDARD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), // 2 nights * $250 = $500 + $50 tax = $550
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 10m,
            Nights = 2,
            TaxesAndFees = 2m,
            TotalAmount = 22m, // Manipulated!
            Currency = "LKR",
            Status = "Draft"
        };
        context.BookingDrafts.Add(tamperedDraft);
        await context.SaveChangesAsync();

        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutRequest = new CheckoutRequest(
            DraftId: tamperedDraft.Id,
            Contact: new ContactInfoDto("Eleanor", "Vance", "+15550199", "guestA@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_visa",
            CouponCode: null
        );

        var result = await checkoutHandler.Handle(
            new CheckoutCommand(checkoutRequest, CustomerId: _guestAId),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Booking price does not match official room rates");
    }

    [Fact]
    public async Task Checkout_CrossGuestDraftAccess_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway();

        // Create draft for Guest A
        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestAId, // Guest A
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STANDARD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "Draft"
        };
        draft.RecalculateTotals();
        context.BookingDrafts.Add(draft);
        await context.SaveChangesAsync();

        // Guest B attempts checkout of Guest A's draft
        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutRequest = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Bob", "Smith", "+15550299", "guestB@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_visa",
            CouponCode: null
        );

        var result = await checkoutHandler.Handle(
            new CheckoutCommand(checkoutRequest, CustomerId: _guestBId), // Authenticated as Guest B
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Unauthorized: this booking draft belongs to another guest");
    }

    [Fact]
    public async Task Checkout_UnauthenticatedCheckoutOfGuestDraft_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway();

        // Create draft bound to Guest A
        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestAId,
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STANDARD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "Draft"
        };
        draft.RecalculateTotals();
        context.BookingDrafts.Add(draft);
        await context.SaveChangesAsync();

        // Anonymous checkout attempt (CustomerId = null)
        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutRequest = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Anon", "User", "+15550299", "anon@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_visa",
            CouponCode: null
        );

        var result = await checkoutHandler.Handle(
            new CheckoutCommand(checkoutRequest, CustomerId: null),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Authentication required: this booking draft belongs to a registered guest");
    }

    [Fact]
    public async Task Checkout_PaymentFailure_CancelsPendingBooking()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var failingGateway = new TestPaymentGateway { Outcome = PaymentExecutionOutcome.ConfirmedDecline, ErrorMessage = "Card declined: insufficient funds" };

        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestAId,
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STANDARD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "Draft"
        };
        draft.RecalculateTotals();
        context.BookingDrafts.Add(draft);
        await context.SaveChangesAsync();

        var checkoutHandler = new CheckoutCommandHandler(context, failingGateway, fakeClient);
        var checkoutRequest = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Eleanor", "Vance", "+15550199", "guestA@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_declined",
            CouponCode: null
        );

        var result = await checkoutHandler.Handle(
            new CheckoutCommand(checkoutRequest, CustomerId: _guestAId),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Card declined");

        // Verify the booking status was set to Cancelled (releasing the room)
        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.PaymentReference == $"DRAFT-{draft.Id}");
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.Cancelled, "Pending reservation must be marked Cancelled upon payment failure");
    }

    [Fact]
    public async Task Checkout_InFlightAttempt_ReturnsInProgressResponseWithoutCallingGateway()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway { Outcome = PaymentExecutionOutcome.Success };

        // Seed draft in PendingPayment state
        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestAId,
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STANDARD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "PendingPayment"
        };
        draft.RecalculateTotals();
        context.BookingDrafts.Add(draft);
        await context.SaveChangesAsync();

        var handler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var request = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Eleanor", "Vance", "+15550199", "guestA@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_card",
            CouponCode: null
        );

        var result = await handler.Handle(new CheckoutCommand(request, _guestAId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("already in progress");
        paymentGateway.ProcessCount.Should().Be(0, "Gateway must not be called when checkout is already in progress");
    }

    [Fact]
    public async Task Checkout_CompletedDraft_ReturnsExistingConfirmationWithoutCallingGateway()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway { Outcome = PaymentExecutionOutcome.Success };

        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestAId,
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STANDARD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "Completed"
        };
        draft.RecalculateTotals();

        var existingBooking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "TH-2026-COMPLETED1",
            CustomerId = _guestAId,
            CustomerLastName = "Vance",
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomNumber = "204",
            RoomTypeId = _roomTypeId,
            CheckInDate = draft.CheckInDate,
            CheckOutDate = draft.CheckOutDate,
            TotalAmount = draft.TotalAmount,
            Status = BookingStatus.Confirmed,
            PaymentReference = $"DRAFT-{draft.Id}"
        };

        context.BookingDrafts.Add(draft);
        context.Bookings.Add(existingBooking);
        await context.SaveChangesAsync();

        var handler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var request = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Eleanor", "Vance", "+15550199", "guestA@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_card",
            CouponCode: null
        );

        var result = await handler.Handle(new CheckoutCommand(request, _guestAId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.BookingReference.Should().Be("TH-2026-COMPLETED1");
        result.Data.ConfirmationMessage.Should().Contain("already confirmed");
        paymentGateway.ProcessCount.Should().Be(0, "Gateway must not be called for an already completed draft");
    }

    [Fact]
    public async Task Checkout_ConcurrentCheckoutOfSameDraft_OnlyOneChargesPaymentAndSucceeds()
    {
        var dbName = Guid.NewGuid().ToString();
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        // Delay ensures task 1 is in-flight in Phase 2 payment while task 2 executes Phase 1
        var paymentGateway = new TestPaymentGateway { Outcome = PaymentExecutionOutcome.Success, DelayMs = 50 };

        // 1. Seed a valid draft
        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestAId,
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STANDARD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "Draft"
        };
        draft.RecalculateTotals();

        using (var seedContext = CreateContext(dbName))
        {
            seedContext.BookingDrafts.Add(draft);
            await seedContext.SaveChangesAsync();
        }

        // 2. Launch two concurrent checkouts of the EXACT SAME draft
        using var context1 = CreateContext(dbName);
        using var context2 = CreateContext(dbName);

        var handler1 = new CheckoutCommandHandler(context1, paymentGateway, fakeClient);
        var handler2 = new CheckoutCommandHandler(context2, paymentGateway, fakeClient);

        var request1 = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Eleanor", "Vance", "+15550199", "guestA@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_card_1",
            CouponCode: null
        );

        var request2 = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Eleanor", "Vance", "+15550199", "guestA@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_card_2",
            CouponCode: null
        );

        var task1 = handler1.Handle(new CheckoutCommand(request1, _guestAId), CancellationToken.None);
        var task2 = handler2.Handle(new CheckoutCommand(request2, _guestAId), CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        successCount.Should().Be(1, "Exactly one concurrent checkout of the same draft must succeed");
        failureCount.Should().Be(1, "The competing in-flight checkout of the same draft must be rejected to prevent duplicate reservation and double-charging");

        // Verify payment was processed only ONCE
        paymentGateway.ProcessCount.Should().Be(1, "Payment gateway must only be invoked once; duplicate checkout must not double-charge the guest");
    }

    private class FaultyBookingDbContext : BookingDbContext
    {
        public bool ThrowOnConfirmationSave { get; set; }

        public FaultyBookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnConfirmationSave)
            {
                var confirmedBooking = ChangeTracker.Entries<Domain.Entities.Booking>()
                    .Any(e => e.Entity.Status == BookingStatus.Confirmed);
                if (confirmedBooking)
                {
                    throw new DbUpdateException("Simulated transient database connection failure during confirmation save.");
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Checkout_DatabaseSaveFailsAfterPayment_HandlesConsistencyGracefully()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using var context = new FaultyBookingDbContext(options);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway { Outcome = PaymentExecutionOutcome.Success, TransactionId = "TXN-CRITICAL-TEST-999" };

        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestAId,
            CustomerEmail = "guestA@example.com",
            RoomId = _roomId,
            RoomName = "Ocean Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STANDARD",
            RatePlanName = "Standard",
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "Draft"
        };
        draft.RecalculateTotals();
        context.BookingDrafts.Add(draft);
        await context.SaveChangesAsync();

        // Enable simulated database failure during Phase 3 confirmation save
        context.ThrowOnConfirmationSave = true;

        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutRequest = new CheckoutRequest(
            DraftId: draft.Id,
            Contact: new ContactInfoDto("Eleanor", "Vance", "+15550199", "guestA@example.com"),
            Address: null,
            SpecialRequests: null,
            Loyalty: null,
            PaymentToken: "tok_valid",
            CouponCode: null
        );

        var result = await checkoutHandler.Handle(
            new CheckoutCommand(checkoutRequest, CustomerId: _guestAId),
            CancellationToken.None);

        // Assert that the system handled it gracefully without throwing an unhandled exception
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("TXN-CRITICAL-TEST-999", "The error message must contain the payment transaction ID so customer & support can trace and reconcile the payment");
        result.Message.Should().Contain("TH-", "The error message must contain the booking reference for support reconciliation");
    }

    // ── Phase 1: Payment-Outcome Classification Tests ──────────────────────────

    [Fact]
    public async Task Checkout_ConfirmedDecline_CancelsPendingBookingAndResetsDraft()
    {
        // When the gateway returns a ConfirmedDecline the booking must be
        // cancelled and the draft must be reset to "Draft" so the guest can
        // retry with a new card. Room must become available again.
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway
        {
            Outcome = PaymentExecutionOutcome.ConfirmedDecline,
            ErrorMessage = "Insufficient funds."
        };

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId, CustomerEmail: "guest@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);
        var draftId = draftResult.Data!.Id;

        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var result = await checkoutHandler.Handle(new CheckoutCommand(
            new CheckoutRequest(draftId,
                new ContactInfoDto("Ana", "Silva", "+1234", "ana@example.com"),
                null, null, null, "tok_valid", null),
            _guestAId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Insufficient funds");

        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.PaymentReference == $"DRAFT-{draftId}");
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.Cancelled,
            "a ConfirmedDecline must cancel the PendingPayment booking to free the room");

        var draft = await context.BookingDrafts.FindAsync(draftId);
        draft!.Status.Should().Be("Draft",
            "a ConfirmedDecline must reset the draft so the guest can retry with a new card");
    }

    [Fact]
    public async Task Checkout_UnknownOutcome_PreservesPendingPaymentAndDoesNotResetDraft()
    {
        // When the gateway returns Unknown the booking MUST remain in
        // PendingPayment and the draft MUST NOT be reset, because the charge
        // may already have been collected.
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway
        {
            Outcome = PaymentExecutionOutcome.Unknown,
            ErrorMessage = "Gateway timeout."
        };

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId, CustomerEmail: "guest@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);
        var draftId = draftResult.Data!.Id;

        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var result = await checkoutHandler.Handle(new CheckoutCommand(
            new CheckoutRequest(draftId,
                new ContactInfoDto("Bo", "Li", "+9999", "bo@example.com"),
                null, null, null, "tok_valid", null),
            _guestAId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("TH-",
            "Unknown outcome message must include the booking reference for support reconciliation");
        result.Message.Should().Contain("contact support");

        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.PaymentReference == $"DRAFT-{draftId}");
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingPayment,
            "Unknown outcome must NOT cancel the booking — the charge may have been collected");

        var draft = await context.BookingDrafts.FindAsync(draftId);
        draft!.Status.Should().Be("PendingPayment",
            "Unknown outcome must NOT reset the draft to Draft — doing so would allow a double-charge retry");
    }

    [Fact]
    public async Task Checkout_HttpRequestException_ClassifiedAsUnknown_PreservesBooking()
    {
        // HttpRequestException (network failure) must be caught and treated
        // as Unknown — not as a ConfirmedDecline — so the room is NOT released.
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway
        {
            ThrowException = new HttpRequestException("Connection refused.")
        };

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId, CustomerEmail: "guest@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);
        var draftId = draftResult.Data!.Id;

        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var result = await checkoutHandler.Handle(new CheckoutCommand(
            new CheckoutRequest(draftId,
                new ContactInfoDto("Cal", "Reed", "+0001", "cal@example.com"),
                null, null, null, "tok_valid", null),
            _guestAId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("TH-",
            "Network-failure response must include the booking reference");

        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.PaymentReference == $"DRAFT-{draftId}");
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingPayment,
            "HttpRequestException must be treated as Unknown — room must not be released");
    }

    [Fact]
    public async Task StaffBooking_OlderThan15Min_BlocksGuestDraftAndCheckout()
    {
        // Case A verification: A staff-created PendingPayment booking older than 15 minutes
        // (CheckoutInitiatedAtUtc = null) MUST continue blocking guest draft creation and checkout.
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway();

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12));

        // Seed a staff-created PendingPayment booking created 35 minutes ago
        var staffBooking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = Domain.Entities.Booking.GenerateBookingReference(),
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "StaffGuest",
            CustomerEmail = "staffguest@example.com",
            RoomId = _roomId,
            RoomNumber = "204",
            RoomTypeId = _roomTypeId,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            GuestCount = 2,
            TotalAmount = 400m,
            Status = BookingStatus.PendingPayment,
            CheckoutInitiatedAtUtc = null, // Staff-created booking
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-35),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-35)
        };
        context.Bookings.Add(staffBooking);
        await context.SaveChangesAsync();

        // Guest attempts to create draft for the same room and dates
        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId,
            CustomerEmail: "guest@example.com",
            RoomId: _roomId,
            RoomName: "Suite",
            RoomTypeId: _roomTypeId,
            RatePlanId: "STD",
            RatePlanName: "Standard",
            CheckInDate: checkIn,
            CheckOutDate: checkOut,
            GuestCount: 2,
            RoomsCount: 1,
            PricePerNight: 200m), CancellationToken.None);

        draftResult.Succeeded.Should().BeFalse(
            "Staff PendingPayment booking older than 15 min must block guest CreateDraft");
        draftResult.Message.Should().Contain("Selected room is not available");

        // Force a draft in DB to test CheckoutCommandHandler's inner conflict check directly
        var draft = new BookingDraft
        {
            Id = Guid.NewGuid(),
            CustomerId = _guestBId,
            CustomerEmail = "guestb@example.com",
            RoomId = _roomId,
            RoomName = "Suite",
            RoomTypeId = _roomTypeId,
            RatePlanId = "STD",
            RatePlanName = "Standard",
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            GuestCount = 2,
            RoomsCount = 1,
            PricePerNight = 200m,
            Currency = "LKR",
            Status = "Draft",
            CreatedAtUtc = DateTime.UtcNow
        };
        draft.RecalculateTotals();
        context.BookingDrafts.Add(draft);
        await context.SaveChangesAsync();

        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutResult = await checkoutHandler.Handle(new CheckoutCommand(
            new CheckoutRequest(draft.Id,
                new ContactInfoDto("Guest", "B", "+1111", "guestb@example.com"),
                null, null, null, "tok_valid", null),
            _guestBId), CancellationToken.None);

        checkoutResult.Succeeded.Should().BeFalse(
            "Staff PendingPayment booking older than 15 min must block CheckoutCommandHandler");
        checkoutResult.Message.Should().Contain("already booked");
    }

    [Fact]
    public async Task UnknownOutcome_HoldsRoomIndefinitely_Past15And60Minutes()
    {
        // Requirement 1 & 2: A booking with an unresolved Unknown payment must hold
        // the room indefinitely. Elapsed time alone must never release the hold.
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway
        {
            Outcome = PaymentExecutionOutcome.Unknown,
            ErrorMessage = "Provider timeout"
        };

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(17));

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId, CustomerEmail: "guest@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);

        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutResult = await checkoutHandler.Handle(new CheckoutCommand(
            new CheckoutRequest(draftResult.Data!.Id,
                new ContactInfoDto("A", "User", "+2222", "a@example.com"),
                null, null, null, "tok_valid", null),
            _guestAId), CancellationToken.None);

        checkoutResult.Succeeded.Should().BeFalse();

        // Simulate time passing: age the booking by 75 minutes
        var heldBooking = await context.Bookings.FirstAsync(b => b.PaymentReference == $"DRAFT-{draftResult.Data!.Id}");
        heldBooking.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-75);
        await context.SaveChangesAsync();

        // Another guest attempts to book the same room for the same dates
        var secondDraftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestBId, CustomerEmail: "guestb@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);

        secondDraftResult.Succeeded.Should().BeFalse(
            "An Unknown outcome booking must continue holding the room even after 75 minutes");
        secondDraftResult.Message.Should().Contain("Selected room is not available");
    }

    [Fact]
    public async Task ConfirmedDecline_ReleasesRoomImmediately()
    {
        // Phase 1 integration: ConfirmedDecline sets Booking.Status = Cancelled,
        // which immediately frees the room for new drafts and bookings.
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway
        {
            Outcome = PaymentExecutionOutcome.ConfirmedDecline,
            ErrorMessage = "Insufficient funds"
        };

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(22));

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId, CustomerEmail: "guest@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);

        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutResult = await checkoutHandler.Handle(new CheckoutCommand(
            new CheckoutRequest(draftResult.Data!.Id,
                new ContactInfoDto("A", "User", "+3333", "a@example.com"),
                null, null, null, "tok_valid", null),
            _guestAId), CancellationToken.None);

        checkoutResult.Succeeded.Should().BeFalse();

        var declinedBooking = await context.Bookings.FirstAsync(b => b.PaymentReference == $"DRAFT-{draftResult.Data!.Id}");
        declinedBooking.Status.Should().Be(BookingStatus.Cancelled);

        // A new draft for the exact same room and dates succeeds immediately
        var newDraftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestBId, CustomerEmail: "guestb@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);

        newDraftResult.Succeeded.Should().BeTrue(
            "Room must be available immediately after ConfirmedDecline cancels the pending booking");
    }

    [Fact]
    public async Task Checkout_StampsCheckoutInitiatedAtUtc()
    {
        // Requirement 6: Verify that CheckoutCommandHandler stamps CheckoutInitiatedAtUtc
        // on the Booking when the Phase 1 reservation transaction executes.
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);
        var paymentGateway = new TestPaymentGateway { Outcome = PaymentExecutionOutcome.Success };

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(27));

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId, CustomerEmail: "guest@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);

        var beforeCheckout = DateTime.UtcNow;
        var checkoutHandler = new CheckoutCommandHandler(context, paymentGateway, fakeClient);
        var checkoutResult = await checkoutHandler.Handle(new CheckoutCommand(
            new CheckoutRequest(draftResult.Data!.Id,
                new ContactInfoDto("A", "User", "+4444", "a@example.com"),
                null, null, null, "tok_valid", null),
            _guestAId), CancellationToken.None);

        checkoutResult.Succeeded.Should().BeTrue();

        var booking = await context.Bookings.FirstAsync(b => b.PaymentReference == $"DRAFT-{draftResult.Data!.Id}");
        booking.CheckoutInitiatedAtUtc.Should().NotBeNull(
            "CheckoutCommandHandler must stamp CheckoutInitiatedAtUtc on the booking");
        booking.CheckoutInitiatedAtUtc!.Value.Should().BeOnOrAfter(beforeCheckout.AddSeconds(-2))
            .And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(2));
    }

    [Fact]
    public async Task LegacyBackfilledBooking_BlocksAvailabilityIndefinitely()
    {
        // Requirement 5: A legacy backfilled PendingPayment booking
        // (CheckoutInitiatedAtUtc = CreatedAtUtc) older than 15 min must block availability.
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var fakeClient = CreateFakeHotelOpsClient(pricePerNight: 200m);

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(33));
        var legacyCreated = DateTime.UtcNow.AddDays(-3);

        var legacyBooking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = Domain.Entities.Booking.GenerateBookingReference(),
            CustomerId = Guid.NewGuid(),
            CustomerLastName = "LegacyGuest",
            CustomerEmail = "legacy@example.com",
            RoomId = _roomId,
            RoomNumber = "204",
            RoomTypeId = _roomTypeId,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            GuestCount = 1,
            TotalAmount = 600m,
            Status = BookingStatus.PendingPayment,
            CheckoutInitiatedAtUtc = legacyCreated, // Backfilled by migration
            CreatedAtUtc = legacyCreated,
            UpdatedAtUtc = legacyCreated
        };
        context.Bookings.Add(legacyBooking);
        await context.SaveChangesAsync();

        var draftHandler = new CreateDraftCommandHandler(context, fakeClient);
        var draftResult = await draftHandler.Handle(new CreateDraftCommand(
            CustomerId: _guestAId, CustomerEmail: "guest@example.com",
            RoomId: _roomId, RoomName: "Suite", RoomTypeId: _roomTypeId,
            RatePlanId: "STD", RatePlanName: "Standard",
            CheckInDate: checkIn, CheckOutDate: checkOut,
            GuestCount: 1, RoomsCount: 1, PricePerNight: 200m), CancellationToken.None);

        draftResult.Succeeded.Should().BeFalse(
            "Legacy backfilled PendingPayment booking must block draft creation regardless of elapsed age");
        draftResult.Message.Should().Contain("Selected room is not available");
    }

    [Fact]
    public void EFCoreMigration_AddCheckoutInitiatedAtUtcAndBackfill_IsDiscoveredByMigrationsAssembly()
    {
        // Non-mutating verification: ensure EF Core's IMigrationsAssembly discovers
        // the AddCheckoutInitiatedAtUtcAndBackfill migration via reflection without connecting to a DB.
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=test_discovery;Username=test;Password=test")
            .Options;

        using var context = new BookingDbContext(options);
        var migrationsAssembly = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>();

        migrationsAssembly.Should().NotBeNull();
        migrationsAssembly.Migrations.Should().ContainKey("20260922180000_AddCheckoutInitiatedAtUtcAndBackfill",
            "EF Core must discover AddCheckoutInitiatedAtUtcAndBackfill via [Migration] attribute");

        var migrationType = migrationsAssembly.Migrations["20260922180000_AddCheckoutInitiatedAtUtcAndBackfill"];
        migrationType.Should().Be(typeof(SmartHotel.Booking.Infrastructure.Persistence.Migrations.AddCheckoutInitiatedAtUtcAndBackfill));
    }
}
