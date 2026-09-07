namespace SmartHotel.Booking.Domain.Policies;

public record RefundCalculationResult(
    decimal RefundAmount,
    decimal FeeAmount,
    decimal RefundPercentage,
    string TierDescription);

public static class RefundPolicy
{
    public const int Tier1DaysThreshold = 10;
    public const decimal Tier1RefundPercentage = 95m; // 5% fee
    public const decimal Tier1FeePercentage = 5m;

    public const int Tier2HoursThreshold = 24;
    public const decimal Tier2RefundPercentage = 90m; // 10% fee
    public const decimal Tier2FeePercentage = 10m;

    public const decimal Tier3RefundPercentage = 50m; // 50% fee
    public const decimal Tier3FeePercentage = 50m;

    public const int DefaultCheckInHour = 14; // Standard 2:00 PM check-in time

    public static RefundCalculationResult CalculateRefund(
        decimal totalAmount,
        DateOnly checkInDate,
        DateTime cancellationTimeUtc)
    {
        if (totalAmount <= 0)
        {
            return new RefundCalculationResult(0, 0, 0, "No refundable amount.");
        }

        var checkInDateTimeUtc = checkInDate.ToDateTime(new TimeOnly(DefaultCheckInHour, 0), DateTimeKind.Utc);
        var timeUntilCheckIn = checkInDateTimeUtc - cancellationTimeUtc;

        decimal refundPercentage;
        string description;

        if (timeUntilCheckIn.TotalDays > Tier1DaysThreshold)
        {
            refundPercentage = Tier1RefundPercentage;
            description = $">10 days prior to check-in: 95% refund (5% processing fee).";
        }
        else if (timeUntilCheckIn.TotalHours > Tier2HoursThreshold)
        {
            refundPercentage = Tier2RefundPercentage;
            description = $"<=10 days and >24 hours prior to check-in: 90% refund (10% processing fee).";
        }
        else
        {
            refundPercentage = Tier3RefundPercentage;
            description = $"<=24 hours prior to check-in: 50% refund (50% processing fee).";
        }

        var refundAmount = Math.Round(totalAmount * (refundPercentage / 100m), 2, MidpointRounding.AwayFromZero);
        var feeAmount = totalAmount - refundAmount;

        return new RefundCalculationResult(refundAmount, feeAmount, refundPercentage, description);
    }
}
