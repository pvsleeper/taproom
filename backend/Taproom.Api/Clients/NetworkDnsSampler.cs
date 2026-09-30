using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>
/// Holds the latest network-wide (no client filter) Unbound query log sample, shared by the dashboard's
/// DNS summary tile and its cross-device naming. Single-flight like InterfaceSampler/WanRateSampler, but
/// with a longer reuse window since the unfiltered log fetch is itself several seconds of paginated calls
/// and neither consumer needs it fresher than that.
/// </summary>
public sealed class NetworkDnsSampler
{
    private static readonly TimeSpan ReuseWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FallbackWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(60);
    private const int MaxRows = 10000;

    private readonly IUnboundLogClient _client;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<NetworkDnsSampler> _logger;

    private IReadOnlyList<DnsQueryEntry> _lastSample = [];
    private DateTimeOffset _lastSuccessAt = DateTimeOffset.MinValue;

    public NetworkDnsSampler(IUnboundLogClient client, ILogger<NetworkDnsSampler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<DnsQueryEntry> Entries, bool Ok)> GetSampleAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return (_lastSample, true);
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return (_lastSample, true);
            }

            try
            {
                using var cts = new CancellationTokenSource(SampleTimeout);
                var entries = await _client.GetAllQueriesAsync(Window, MaxRows, cts.Token);
                _lastSample = entries;
                _lastSuccessAt = DateTimeOffset.UtcNow;
                return (entries, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Network-wide DNS query sample failed");
                if (DateTimeOffset.UtcNow - _lastSuccessAt < FallbackWindow)
                {
                    return (_lastSample, false);
                }
                _lastSample = [];
                return ([], false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsFresh() => _lastSuccessAt != DateTimeOffset.MinValue && DateTimeOffset.UtcNow - _lastSuccessAt < ReuseWindow;
}
