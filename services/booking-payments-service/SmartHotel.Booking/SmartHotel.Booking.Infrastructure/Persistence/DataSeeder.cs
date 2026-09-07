using Microsoft.EntityFrameworkCore;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;

namespace SmartHotel.Booking.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(BookingDbContext context)
    {
        if (await context.Bookings.AnyAsync())
        {
            return;
        }

        var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var roomId = Guid.Parse("33333333-3333-3333-3333-333333333331");
        var roomTypeId = Guid.Parse("22222222-2222-2222-2222-222222222221");

        var sampleBooking = new Domain.Entities.Booking
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444441"),
            BookingReference = "TH-2026-DEMO01",
            CustomerId = customerId,
            CustomerLastName = "Perera",
            CustomerEmail = "guest@example.com",
            RoomId = roomId,
            RoomTypeId = roomTypeId,
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            GuestCount = 2,
            TotalAmount = 45000.00m,
            Status = BookingStatus.CheckedOut,
            PaymentReference = "PAY-DEMO-001",
            PayHereOrderId = "TH-2026-DEMO01"
        };

        var samplePayment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = sampleBooking.Id,
            Amount = 45000.00m,
            Currency = "LKR",
            Provider = PaymentProvider.PayHere,
            PayHereOrderId = "TH-2026-DEMO01",
            PayHerePaymentId = "PH-DEMO-9999",
            Status = PaymentStatus.Completed,
            CapturedAt = DateTime.UtcNow.AddDays(-3)
        };

        var sampleReview = new Review
        {
            Id = Guid.NewGuid(),
            BookingId = sampleBooking.Id,
            CustomerId = customerId,
            RoomTypeId = roomTypeId,
            Rating = 5,
            Comment = "Exceptional ocean view suite! Clean, luxurious, and top-notch service.",
            IsPublished = true
        };

        context.Bookings.Add(sampleBooking);
        context.Payments.Add(samplePayment);
        context.Reviews.Add(sampleReview);

        await context.SaveChangesAsync();
    }
}
