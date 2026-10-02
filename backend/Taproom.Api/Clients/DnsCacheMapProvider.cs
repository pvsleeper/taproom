using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>
/// Holds the IP → domain map built from Unbound's cache. Refreshed at most once a minute, shared by every
/// page (single-flight, so N open pages cause one upstream fetch, not N). On failure the last good map keeps
/// being served and the next attempt waits a short while rather than re-fetching ~1.7 MB on every request.
/// </summary>
public sealed class DnsCacheMapProvider
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(30);

    private readonly IUnboundCacheClient _client;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<DnsCacheMapProvider> _logger;

    private DnsCacheMap _map = DnsCacheMap.Empty;
    private bool _lastFetchOk;
    private DateTimeOffset _nextAttemptAt = DateTimeOffset.MinValue;

    public DnsCacheMapProvider(IUnboundCacheClient client, ILogger<DnsCacheMapProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(DnsCacheMap Map, bool Ok)> GetMapAsync(CancellationToken cancellationToken)
    {
        if (!IsDue())
        {
            return (_map, _lastFetchOk);
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!IsDue())
            {
                return (_map, _lastFetchOk);
            }

            try
            {
                // Not linked to the caller's token: the fetch is shared by every waiter.
                using var cts = new CancellationTokenSource(FetchTimeout);
                var records = await _client.GetRecordsAsync(cts.Token);
                _map = DnsCacheMap.Build(records);
                _lastFetchOk = true;
                _nextAttemptAt = DateTimeOffset.UtcNow + RefreshInterval;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unbound cache dump failed; keeping the previous IP-to-domain map");
                _lastFetchOk = false;
                _nextAttemptAt = DateTimeOffset.UtcNow + RetryAfterFailure;
            }

            return (_map, _lastFetchOk);
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsDue() => DateTimeOffset.UtcNow >= _nextAttemptAt;
}
