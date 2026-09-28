using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Taproom.Api.Config;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>
/// Orchestrates a client's live connections on demand: fetches firewall states and recent DNS queries,
/// resolves names and locations, runs them through <see cref="ConnectionEnricher"/>, and caches the
/// result for a few seconds so several open tabs on one device cause only one upstream round of calls.
/// </summary>
public sealed class ConnectionsService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DomainLookbackWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(10);

    private readonly IStatesClient _states;
    private readonly IUnboundLogClient _unboundLog;
    private readonly DomainResolver _domainResolver;
    private readonly GeoIpService _geoIp;
    private readonly ClientSnapshotProvider _snapshotProvider;
    private readonly IMemoryCache _cache;
    private readonly HomeOptions _home;
    private readonly ILogger<ConnectionsService> _logger;

    // Reused when a fetch fails (e.g. OPNsense's web backend occasionally corrupts a large chunked
    // response), so one transient blip doesn't empty the whole connections view — same pattern as
    // phase 1's poller reusing last-good source data.
    private readonly ConcurrentDictionary<string, IReadOnlyList<FirewallState>> _lastStatesByIp = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<DnsQueryEntry>> _lastQueriesByIp = new();

    public ConnectionsService(
        IStatesClient states,
        IUnboundLogClient unboundLog,
        DomainResolver domainResolver,
        GeoIpService geoIp,
        ClientSnapshotProvider snapshotProvider,
        IMemoryCache cache,
        IOptions<TaproomOptions> options,
        ILogger<ConnectionsService> logger)
    {
        _states = states;
        _unboundLog = unboundLog;
        _domainResolver = domainResolver;
        _geoIp = geoIp;
        _snapshotProvider = snapshotProvider;
        _cache = cache;
        _home = options.Value.Home;
        _logger = logger;
    }

    public async Task<ConnectionsResult?> GetConnectionsAsync(string mac, CancellationToken cancellationToken)
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);
        var client = snapshot.Clients.FirstOrDefault(c => c.Mac == mac);
        if (client is null)
        {
            return null;
        }

        var home = new HomeLocation { Lat = _home.Latitude, Lon = _home.Longitude };

        if (client.Ip is null)
        {
            return new ConnectionsResult
            {
                Mac = mac,
                Ip = null,
                GeneratedAt = DateTimeOffset.UtcNow,
                Home = home,
                Connections = [],
                Markers = [],
                Local = [],
                UnknownLocation = [],
                Hints = [],
                Sources = new Dictionary<string, SourceStatus>
                {
                    ["states"] = new() { Ok = true },
                    ["dns"] = new() { Ok = true },
                    ["geoip"] = new() { Ok = _geoIp.Available },
                },
            };
        }

        var cacheKey = $"connections:{mac}:{client.Ip}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await BuildAsync(mac, client.Ip, home, snapshot.Clients, cancellationToken);
        }) ?? throw new InvalidOperationException("Cache factory returned null.");
    }

    private async Task<ConnectionsResult> BuildAsync(
        string mac, string ip, HomeLocation home, IReadOnlyList<NetworkClient> allClients, CancellationToken cancellationToken)
    {
        var statesOk = true;
        string? statesError = null;
        IReadOnlyList<FirewallState> states = _lastStatesByIp.GetValueOrDefault(ip, []);
        try
        {
            using var cts = new CancellationTokenSource(SourceTimeout);
            states = await _states.GetStatesForClientAsync(ip, cts.Token);
            _lastStatesByIp[ip] = states;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firewall states fetch failed for {Ip}", ip);
            statesOk = false;
            statesError = ex.Message;
        }

        var dnsOk = true;
        string? dnsError = null;
        IReadOnlyList<DnsQueryEntry> recentQueries = _lastQueriesByIp.GetValueOrDefault(ip, []);
        try
        {
            using var cts = new CancellationTokenSource(SourceTimeout);
            recentQueries = await _unboundLog.GetQueriesForClientAsync(ip, DomainLookbackWindow, cts.Token);
            _lastQueriesByIp[ip] = recentQueries;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unbound query log fetch failed for {Ip}", ip);
            dnsOk = false;
            dnsError = ex.Message;
        }

        var recentDomains = recentQueries
            .Where(q => q.Type is "A" or "AAAA")
            .GroupBy(q => q.Domain)
            .Select(g => new { Domain = g.Key, LastSeen = g.Max(q => q.Time) })
            .OrderByDescending(g => g.LastSeen)
            .Select(g => g.Domain)
            .ToList();

        // Resolve from whatever domains we have, even stale ones from a failed fetch — better than nothing.
        var dnsNamesByIp = await _domainResolver.ResolveDomainsAsync(recentDomains, cancellationToken);

        var publicRemoteIps = states
            .Select(s => s.RemoteIp)
            .Distinct()
            .Where(remoteIp => !PrivateIp.IsPrivateOrLinkLocal(remoteIp))
            .ToList();

        // Independent per-IP lookups (each usually a cache hit after the first pass), run concurrently.
        var needsPtr = publicRemoteIps.Where(remoteIp => !dnsNamesByIp.ContainsKey(remoteIp)).ToList();
        var ptrResults = await Task.WhenAll(needsPtr.Select(async ip => (ip, ptr: await _domainResolver.ReversePtrAsync(ip, cancellationToken))));
        var ptrNamesByIp = ptrResults.ToDictionary(r => r.ip, r => r.ptr);

        var geoByIp = new Dictionary<string, GeoInfo>();
        foreach (var remoteIp in publicRemoteIps)
        {
            var geo = _geoIp.Lookup(remoteIp);
            if (geo is not null)
            {
                geoByIp[remoteIp] = geo;
            }
        }

        var localNamesByIp = allClients
            .Where(c => c.Ip is not null)
            .GroupBy(c => c.Ip!)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var outcome = ConnectionEnricher.Enrich(states, dnsNamesByIp, ptrNamesByIp, geoByIp, localNamesByIp, recentQueries.Count);

        return new ConnectionsResult
        {
            Mac = mac,
            Ip = ip,
            GeneratedAt = DateTimeOffset.UtcNow,
            Home = home,
            Connections = outcome.Connections,
            Markers = outcome.Markers,
            Local = outcome.Local,
            UnknownLocation = outcome.UnknownLocation,
            Hints = outcome.Hints,
            Sources = new Dictionary<string, SourceStatus>
            {
                ["states"] = new() { Ok = statesOk, Error = statesError },
                ["dns"] = new() { Ok = dnsOk, Error = dnsError },
                ["geoip"] = new() { Ok = _geoIp.Available },
            },
        };
    }
}
