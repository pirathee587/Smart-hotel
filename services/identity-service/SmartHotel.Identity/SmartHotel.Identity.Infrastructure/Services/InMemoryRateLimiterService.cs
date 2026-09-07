using System.Collections.Concurrent;
using SmartHotel.Identity.Application.Interfaces;

namespace SmartHotel.Identity.Infrastructure.Services;

public class InMemoryRateLimiterService : IRateLimiterService
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _attempts = new();
    private readonly object _lock = new();

    public bool CheckAndRecordAttempt(string key, int maxAttempts, TimeSpan window)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var windowStart = now - window;

            var list = _attempts.GetOrAdd(key, _ => new List<DateTime>());
            
            // Remove attempts older than the sliding window
            list.RemoveAll(ts => ts < windowStart);

            if (list.Count >= maxAttempts)
            {
                return false;
            }

            list.Add(now);
            return true;
        }
    }

    public TimeSpan? GetRemainingLockout(string key, int maxAttempts, TimeSpan window)
    {
        lock (_lock)
        {
            if (!_attempts.TryGetValue(key, out var list))
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var windowStart = now - window;
            list.RemoveAll(ts => ts < windowStart);

            if (list.Count < maxAttempts)
            {
                return null;
            }

            var oldestAttempt = list.Min();
            var expiresAt = oldestAttempt + window;
            var remaining = expiresAt - now;

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
