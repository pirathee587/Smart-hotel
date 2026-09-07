using FluentAssertions;
using SmartHotel.Booking.Domain.Policies;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class RefundPolicyTests
{
    private const decimal TotalAmount = 100000.00m; // 100,000 LKR
    private readonly DateOnly _checkInDate = new(2026, 10, 20);

    [Fact]
    public void CalculateRefund_MoreThan10DaysBeforeCheckIn_Returns95PercentRefund()
    {
        // 15 days before check-in
        var cancellationTime = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        var result = RefundPolicy.CalculateRefund(TotalAmount, _checkInDate, cancellationTime);

        result.RefundPercentage.Should().Be(95m);
        result.RefundAmount.Should().Be(95000.00m);
        result.FeeAmount.Should().Be(5000.00m);
        result.TierDescription.Should().Contain("95% refund");
    }

    [Fact]
    public void CalculateRefund_11DaysBeforeCheckIn_Returns95PercentRefund()
    {
        // 11 days before check-in
        var cancellationTime = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);

        var result = RefundPolicy.CalculateRefund(TotalAmount, _checkInDate, cancellationTime);

        result.RefundPercentage.Should().Be(95m);
        result.RefundAmount.Should().Be(95000.00m);
        result.FeeAmount.Should().Be(5000.00m);
    }

    [Fact]
    public void CalculateRefund_Exactly10DaysBeforeCheckIn_Returns90PercentRefund()
    {
        // <= 10 days and > 24 hours: 10 days exactly (check-in is at 14:00)
        var cancellationTime = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc);

        var result = RefundPolicy.CalculateRefund(TotalAmount, _checkInDate, cancellationTime);

        result.RefundPercentage.Should().Be(90m);
        result.RefundAmount.Should().Be(90000.00m);
        result.FeeAmount.Should().Be(10000.00m);
        result.TierDescription.Should().Contain("90% refund");
    }

    [Fact]
    public void CalculateRefund_5DaysBeforeCheckIn_Returns90PercentRefund()
    {
        // 5 days before check-in
        var cancellationTime = new DateTime(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc);

        var result = RefundPolicy.CalculateRefund(TotalAmount, _checkInDate, cancellationTime);

        result.RefundPercentage.Should().Be(90m);
        result.RefundAmount.Should().Be(90000.00m);
        result.FeeAmount.Should().Be(10000.00m);
    }

    [Fact]
    public void CalculateRefund_25HoursBeforeCheckIn_Returns90PercentRefund()
    {
        // 25 hours before check-in (14:00 on 2026-10-20 -> 2026-10-19 13:00:00)
        var cancellationTime = new DateTime(2026, 10, 19, 13, 0, 0, DateTimeKind.Utc);

        var result = RefundPolicy.CalculateRefund(TotalAmount, _checkInDate, cancellationTime);

        result.RefundPercentage.Should().Be(90m);
        result.RefundAmount.Should().Be(90000.00m);
        result.FeeAmount.Should().Be(10000.00m);
    }

    [Fact]
    public void CalculateRefund_Exactly24HoursBeforeCheckIn_Returns50PercentRefund()
    {
        // <= 24 hours before check-in (14:00 on 2026-10-20 -> 2026-10-19 14:00:00)
        var cancellationTime = new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc);

        var result = RefundPolicy.CalculateRefund(TotalAmount, _checkInDate, cancellationTime);

        result.RefundPercentage.Should().Be(50m);
        result.RefundAmount.Should().Be(50000.00m);
        result.FeeAmount.Should().Be(50000.00m);
        result.TierDescription.Should().Contain("50% refund");
    }

    [Fact]
    public void CalculateRefund_6HoursBeforeCheckIn_Returns50PercentRefund()
    {
        // 6 hours before check-in (2026-10-19 18:00:00)
        var cancellationTime = new DateTime(2026, 10, 19, 18, 0, 0, DateTimeKind.Utc);

        var result = RefundPolicy.CalculateRefund(TotalAmount, _checkInDate, cancellationTime);

        result.RefundPercentage.Should().Be(50m);
        result.RefundAmount.Should().Be(50000.00m);
        result.FeeAmount.Should().Be(50000.00m);
    }
}
