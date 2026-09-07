namespace SmartHotel.Identity.Application.Interfaces;

public interface IRateLimiterService
{
    /// <summary>
    /// Checks if an attempt is permitted within the given sliding or fixed window, and records the attempt if permitted.
    /// Returns true if permitted, false if rate limit has been reached.
    /// </summary>
    bool CheckAndRecordAttempt(string key, int maxAttempts, TimeSpan window);

    /// <summary>
    /// Gets the duration remaining until the rate-limited key resets or allows new attempts.
    /// </summary>
    TimeSpan? GetRemainingLockout(string key, int maxAttempts, TimeSpan window);
}
