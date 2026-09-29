using Microsoft.Extensions.Caching.Memory;

namespace Taproom.Api.Clients;

public sealed record ConnectionRate
{
    public long? DownBps { get; init; }
    public long? UpBps { get; init; }
}

/// <summary>
/// Turns each connection's cumulative byte counters into a live rate by comparing them against the
/// previous reading. Previous readings live in IMemoryCache with a 30-second sliding expiry, so a
/// connection that closes (and stops being reported) falls out on its own — no explicit cleanup needed.
/// </summary>
public sealed class ConnectionRateTracker
{
    private static readonly TimeSpan SlidingExpiry = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;

    public ConnectionRateTracker(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <param name="key">Unique per connection — e.g. mac + remote IP + remote port + protocol.</param>
    public ConnectionRate Update(string key, long downBytes, long upBytes, DateTimeOffset now)
    {
        var cacheKey = $"connrate:{key}";
        var previous = _cache.Get<Reading>(cacheKey);
        _cache.Set(cacheKey, new Reading(downBytes, upBytes, now), SlidingExpiry);

        if (previous is null)
        {
            return new ConnectionRate();
        }

        var elapsed = (now - previous.At).TotalSeconds;
        if (elapsed <= 0)
        {
            return new ConnectionRate();
        }

        // A counter that dropped means the connection was replaced (state table reused the slot) —
        // treat it as a fresh reading rather than showing a nonsensical negative rate.
        if (downBytes < previous.DownBytes || upBytes < previous.UpBytes)
        {
            return new ConnectionRate();
        }

        var downBps = (long)((downBytes - previous.DownBytes) * 8 / elapsed);
        var upBps = (long)((upBytes - previous.UpBytes) * 8 / elapsed);
        return new ConnectionRate { DownBps = downBps, UpBps = upBps };
    }

    private sealed record Reading(long DownBytes, long UpBytes, DateTimeOffset At);
}
