using FluentAssertions;
using SmartHotel.Identity.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Identity.UnitTests;

public class RateLimiterTests
{
    [Fact]
    public void CheckAndRecordAttempt_TriggersAtSixthAttempt_WithinWindow()
    {
        var rateLimiter = new InMemoryRateLimiterService();
        var key = "test-rate-limit-key";
        var window = TimeSpan.FromHours(1);

        // Attempts 1 to 5 should succeed
        for (int i = 1; i <= 5; i++)
        {
            var allowed = rateLimiter.CheckAndRecordAttempt(key, maxAttempts: 5, window);
            allowed.Should().BeTrue($"Attempt {i} within limit should be allowed");
        }

        // Attempt 6 should be rejected
        var sixthAttemptAllowed = rateLimiter.CheckAndRecordAttempt(key, maxAttempts: 5, window);
        sixthAttemptAllowed.Should().BeFalse("Attempt 6 should trigger rate limit block");

        // Lockout remaining should be positive
        var remaining = rateLimiter.GetRemainingLockout(key, maxAttempts: 5, window);
        remaining.Should().NotBeNull();
        remaining!.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
