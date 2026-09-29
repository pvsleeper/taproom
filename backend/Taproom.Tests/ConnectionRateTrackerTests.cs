using Microsoft.Extensions.Caching.Memory;
using Taproom.Api.Clients;

namespace Taproom.Tests;

public class ConnectionRateTrackerTests
{
    private static ConnectionRateTracker NewTracker() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void First_reading_for_a_key_has_no_rate()
    {
        var tracker = NewTracker();
        var rate = tracker.Update("k1", downBytes: 1000, upBytes: 500, DateTimeOffset.UtcNow);

        Assert.Null(rate.DownBps);
        Assert.Null(rate.UpBps);
    }

    [Fact]
    public void Second_reading_computes_bits_per_second_from_the_byte_delta()
    {
        var tracker = NewTracker();
        var t0 = DateTimeOffset.UtcNow;
        tracker.Update("k1", downBytes: 1000, upBytes: 500, t0);

        var rate = tracker.Update("k1", downBytes: 2000, upBytes: 1500, t0.AddSeconds(2));

        // (2000-1000)*8/2 = 4000 bps down; (1500-500)*8/2 = 4000 bps up.
        Assert.Equal(4000, rate.DownBps);
        Assert.Equal(4000, rate.UpBps);
    }

    [Fact]
    public void A_counter_that_drops_is_treated_as_a_new_connection_not_a_negative_rate()
    {
        var tracker = NewTracker();
        var t0 = DateTimeOffset.UtcNow;
        tracker.Update("k1", downBytes: 5000, upBytes: 5000, t0);

        // Counter reset — e.g. the state table slot was reused by a new connection.
        var rate = tracker.Update("k1", downBytes: 100, upBytes: 50, t0.AddSeconds(2));

        Assert.Null(rate.DownBps);
        Assert.Null(rate.UpBps);
    }

    [Fact]
    public void Different_keys_are_tracked_independently()
    {
        var tracker = NewTracker();
        var t0 = DateTimeOffset.UtcNow;
        tracker.Update("k1", downBytes: 1000, upBytes: 0, t0);
        tracker.Update("k2", downBytes: 9000, upBytes: 0, t0);

        var rateK1 = tracker.Update("k1", downBytes: 1100, upBytes: 0, t0.AddSeconds(1));
        var rateK2 = tracker.Update("k2", downBytes: 9800, upBytes: 0, t0.AddSeconds(1));

        Assert.Equal(800, rateK1.DownBps);
        Assert.Equal(6400, rateK2.DownBps);
    }

    [Fact]
    public void A_key_that_falls_out_of_the_cache_is_treated_as_a_first_reading_again()
    {
        // Simulates the 30-second sliding expiry evicting a closed connection's entry: once the cache
        // no longer has a previous reading, the next update for that key must behave like a fresh one.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new ConnectionRateTracker(cache);
        var t0 = DateTimeOffset.UtcNow;

        tracker.Update("k1", downBytes: 1000, upBytes: 0, t0);
        cache.Remove("connrate:k1");

        var rate = tracker.Update("k1", downBytes: 1100, upBytes: 0, t0.AddSeconds(1));

        Assert.Null(rate.DownBps);
    }
}
