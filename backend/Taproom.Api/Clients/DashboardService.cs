using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Taproom.Api.Config;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>
/// Orchestrates the four dashboard endpoints. Each is cached briefly (via IMemoryCache) so several open
/// dashboard tabs cause the same number of upstream calls as one, same pattern as ConnectionsService.
/// </summary>
public sealed class DashboardService
{
    // How long a page load waits on uncached reverse lookups; slower ones finish in the background.
    private static readonly TimeSpan PtrBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SummaryCacheDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ConnectionsCacheDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TopTalkersCacheDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(15);

    private readonly IStatesClient _states;
    private readonly IOpnsenseClient _opnsense;
    private readonly PtrResolver _ptrResolver;
    private readonly DnsCacheMapProvider _dnsCache;
    private readonly GeoIpService _geoIp;
    private readonly ClientSnapshotProvider _snapshotProvider;
    private readonly InterfaceSampler _interfaceSampler;
    private readonly WanRateSampler _wanRateSampler;
    private readonly NetworkDnsSampler _dnsSampler;
    private readonly ConnectionRateTracker _rateTracker;
    private readonly IMemoryCache _cache;
    private readonly HomeOptions _home;
    private readonly ILogger<DashboardService> _logger;

    private IReadOnlyList<FirewallState> _lastStates = [];

    public DashboardService(
        IStatesClient states,
        IOpnsenseClient opnsense,
        PtrResolver ptrResolver,
        DnsCacheMapProvider dnsCache,
        GeoIpService geoIp,
        ClientSnapshotProvider snapshotProvider,
        InterfaceSampler interfaceSampler,
        WanRateSampler wanRateSampler,
        NetworkDnsSampler dnsSampler,
        ConnectionRateTracker rateTracker,
        IMemoryCache cache,
        IOptions<TaproomOptions> options,
        ILogger<DashboardService> logger)
    {
        _states = states;
        _opnsense = opnsense;
        _ptrResolver = ptrResolver;
        _dnsCache = dnsCache;
        _geoIp = geoIp;
        _snapshotProvider = snapshotProvider;
        _interfaceSampler = interfaceSampler;
        _wanRateSampler = wanRateSampler;
        _dnsSampler = dnsSampler;
        _rateTracker = rateTracker;
        _cache = cache;
        _home = options.Value.Home;
        _logger = logger;
    }

    public async Task<DashboardSummaryResult> GetSummaryAsync(CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync("dashboard:summary", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SummaryCacheDuration;
            return await BuildSummaryAsync(cancellationToken);
        }) ?? throw new InvalidOperationException("Cache factory returned null.");

    private async Task<DashboardSummaryResult> BuildSummaryAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);
        var online = snapshot.Clients.Count(c => c.Online);
        var wireless = snapshot.Clients.Count(c => c.Online && c.Connection == ConnectionType.Wireless);
        var wired = snapshot.Clients.Count(c => c.Online && c.Connection == ConnectionType.Wired);

        var (dnsEntries, dnsOk) = await _dnsSampler.GetSampleAsync(cancellationToken);

        return new DashboardSummaryResult
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Clients = new ClientCounts { Online = online, Wireless = wireless, Wired = wired },
            Dns = new DashboardDnsSummary
            {
                Queries = dnsEntries.Count,
                Blocked = dnsEntries.Count(e => e.Blocked),
                Failed = dnsEntries.Count(e => e.Failed),
            },
            Sources = new Dictionary<string, SourceStatus>
            {
                ["clients"] = new() { Ok = true },
                ["dns"] = new() { Ok = dnsOk },
            },
        };
    }

    public async Task<DashboardWanResult> GetWanAsync(CancellationToken cancellationToken)
    {
        var (down, up, ok) = await _wanRateSampler.GetRateAsync(cancellationToken);
        return new DashboardWanResult { SampledAt = DateTimeOffset.UtcNow, DownBps = down, UpBps = up, Ok = ok };
    }

    public async Task<DashboardConnectionsResult> GetConnectionsAsync(CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync("dashboard:connections", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ConnectionsCacheDuration;
            return await BuildConnectionsAsync(cancellationToken);
        }) ?? throw new InvalidOperationException("Cache factory returned null.");

    private async Task<DashboardConnectionsResult> BuildConnectionsAsync(CancellationToken cancellationToken)
    {
        var home = new HomeLocation { Lat = _home.Latitude, Lon = _home.Longitude };
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);

        var statesOk = true;
        string? statesError = null;
        var states = _lastStates;
        try
        {
            using var cts = new CancellationTokenSource(SourceTimeout);
            states = await _states.GetAllStatesAsync(cts.Token);
            _lastStates = states;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Network-wide firewall states fetch failed");
            statesOk = false;
            statesError = ex.Message;
        }

        var ndp = await _opnsense.GetNdpTableAsync(cancellationToken);
        var addressMap = DeviceAttributor.BuildAddressMap(snapshot, ndp);

        var (dnsEntries, dnsOk) = await _dnsSampler.GetSampleAsync(cancellationToken);
        var domains = NetworkDnsNaming.ExtractDomainsMostRecentFirst(dnsEntries);

        var publicRemoteIps = states
            .Select(s => s.RemoteIp)
            .Distinct()
            .Where(remoteIp => !PrivateIp.IsPrivateOrLinkLocal(remoteIp))
            .ToList();

        var (cacheMap, cacheOk) = await _dnsCache.GetMapAsync(cancellationToken);
        var dnsNamesByIp = DnsNaming.ChooseNames(cacheMap, publicRemoteIps, domains);

        var needsPtr = publicRemoteIps.Where(remoteIp => !dnsNamesByIp.ContainsKey(remoteIp)).ToList();
        var ptrNamesByIp = await _ptrResolver.ResolveManyAsync(needsPtr, PtrBudget, cancellationToken);

        var geoByIp = new Dictionary<string, GeoInfo>();
        foreach (var remoteIp in publicRemoteIps)
        {
            var geo = _geoIp.Lookup(remoteIp);
            if (geo is not null)
            {
                geoByIp[remoteIp] = geo;
            }
        }

        var outcome = NetworkWideConnectionEnricher.Enrich(states, dnsNamesByIp, ptrNamesByIp, geoByIp, addressMap);

        var now = DateTimeOffset.UtcNow;
        var ratedConnections = outcome.Connections
            .Select(c =>
            {
                // Keyed the same way as the per-client detail page (mac:remoteIp:port:protocol), so the
                // dashboard and a device's own detail page share previous readings for the same connection.
                var key = $"{c.Mac}:{c.RemoteIp}:{c.RemotePort}:{c.Protocol}";
                var rate = _rateTracker.Update(key, c.DownBytes, c.UpBytes, now);
                return c with { DownBps = rate.DownBps, UpBps = rate.UpBps };
            })
            .ToList();

        var rateByMarkerKey = ratedConnections
            .GroupBy(c => ConnectionEnricher.MarkerKeyFor(c.Lat!.Value, c.Lon!.Value))
            .ToDictionary(g => g.Key, g => (Down: g.Sum(c => c.DownBps ?? 0), Up: g.Sum(c => c.UpBps ?? 0)));

        var devicesByMarkerKey = ratedConnections
            .GroupBy(c => ConnectionEnricher.MarkerKeyFor(c.Lat!.Value, c.Lon!.Value))
            .ToDictionary(g => g.Key, g => g
                .GroupBy(c => (c.Mac!, c.DeviceName!))
                .Select(dg => new MarkerDeviceBreakdown
                {
                    Mac = dg.Key.Item1,
                    Name = dg.Key.Item2,
                    ConnectionCount = dg.Count(),
                    DownBps = dg.Sum(c => c.DownBps ?? 0),
                    UpBps = dg.Sum(c => c.UpBps ?? 0),
                })
                .ToList() as IReadOnlyList<MarkerDeviceBreakdown>);

        var ratedMarkers = outcome.Markers
            .Select(m =>
            {
                var (down, up) = rateByMarkerKey.GetValueOrDefault(m.Key, (0, 0));
                var devices = devicesByMarkerKey.GetValueOrDefault(m.Key, m.Devices);
                return m with { DownBps = down, UpBps = up, Devices = devices };
            })
            .ToList();

        return new DashboardConnectionsResult
        {
            GeneratedAt = now,
            Home = home,
            Connections = ratedConnections,
            Markers = ratedMarkers,
            Sources = new Dictionary<string, SourceStatus>
            {
                ["states"] = new() { Ok = statesOk, Error = statesError },
                ["dns"] = new() { Ok = dnsOk },
                ["dnscache"] = new() { Ok = cacheOk },
                ["geoip"] = new() { Ok = _geoIp.Available },
            },
        };
    }

    public async Task<TopTalkersResult> GetTopTalkersAsync(int limit, CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync($"dashboard:top-talkers:{limit}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TopTalkersCacheDuration;
            return await BuildTopTalkersAsync(limit, cancellationToken);
        }) ?? throw new InvalidOperationException("Cache factory returned null.");

    private async Task<TopTalkersResult> BuildTopTalkersAsync(int limit, CancellationToken cancellationToken)
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);
        var ndp = await _opnsense.GetNdpTableAsync(cancellationToken);
        var addressMap = DeviceAttributor.BuildAddressMap(snapshot, ndp);

        var (records, ok) = await _interfaceSampler.GetSampleAsync(cancellationToken);
        var devices = TopTalkersAggregator.Aggregate(records, addressMap, limit);

        return new TopTalkersResult { SampledAt = DateTimeOffset.UtcNow, Devices = devices, Ok = ok };
    }
}
