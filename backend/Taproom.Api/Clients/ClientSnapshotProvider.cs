using Microsoft.Extensions.Options;
using Taproom.Api.Config;
using Taproom.Api.Sources.Omada;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>
/// Fetches Omada and OPNsense on demand and caches the merged snapshot for <c>CacheSeconds</c>, rather
/// than polling on a background timer. Concurrent requests during a cache miss share a single upstream
/// fetch. If a source fails, its last good data is reused and the source is marked unhealthy rather
/// than emptying the table.
/// </summary>
public sealed class ClientSnapshotProvider
{
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(10);

    private readonly IOmadaClient _omada;
    private readonly IOpnsenseClient _opnsense;
    private readonly TaproomOptions _options;
    private readonly ILogger<ClientSnapshotProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private ClientSnapshot? _cached;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    private IReadOnlyList<OmadaClientRecord> _lastOmada = [];
    private IReadOnlyList<ArpEntry> _lastArp = [];
    private IReadOnlyList<DhcpLease> _lastDhcp = [];
    private SourceStatus _omadaStatus = new() { Ok = false };
    private SourceStatus _opnsenseStatus = new() { Ok = false };

    public ClientSnapshotProvider(
        IOmadaClient omada,
        IOpnsenseClient opnsense,
        IOptions<TaproomOptions> options,
        ILogger<ClientSnapshotProvider> logger)
    {
        _omada = omada;
        _opnsense = opnsense;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ClientSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return _cached!;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return _cached!;
            }

            // Not linked to the caller's own cancellationToken: this fetch is shared by every concurrent
            // caller waiting on the lock above, and one caller disconnecting shouldn't abort it for the rest.
            var omadaTask = FetchOmadaAsync();
            var opnsenseTask = FetchOpnsenseAsync();
            await Task.WhenAll(omadaTask, opnsenseTask);

            var snapshot = new ClientSnapshot
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                Sources = new Dictionary<string, SourceStatus>
                {
                    ["omada"] = _omadaStatus,
                    ["opnsense"] = _opnsenseStatus,
                },
                Clients = ClientMerger.Merge(_lastOmada, _lastArp, _lastDhcp),
            };

            _cached = snapshot;
            _cachedAt = DateTimeOffset.UtcNow;
            return snapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool IsFresh() =>
        _cached is not null && DateTimeOffset.UtcNow - _cachedAt < TimeSpan.FromSeconds(_options.CacheSeconds);

    private async Task FetchOmadaAsync()
    {
        using var cts = new CancellationTokenSource(SourceTimeout);
        try
        {
            _lastOmada = await _omada.GetClientsAsync(cts.Token);
            _omadaStatus = new SourceStatus { Ok = true, LastSuccess = DateTimeOffset.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Omada fetch failed");
            _omadaStatus = _omadaStatus with { Ok = false, Error = ex.Message };
        }
    }

    private async Task FetchOpnsenseAsync()
    {
        using var cts = new CancellationTokenSource(SourceTimeout);
        try
        {
            var arpTask = _opnsense.GetArpTableAsync(cts.Token);
            var dhcpTask = _opnsense.GetDhcpLeasesAsync(cts.Token);
            await Task.WhenAll(arpTask, dhcpTask);
            _lastArp = arpTask.Result;
            _lastDhcp = dhcpTask.Result;
            _opnsenseStatus = new SourceStatus { Ok = true, LastSuccess = DateTimeOffset.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OPNsense fetch failed");
            _opnsenseStatus = _opnsenseStatus with { Ok = false, Error = ex.Message };
        }
    }
}
