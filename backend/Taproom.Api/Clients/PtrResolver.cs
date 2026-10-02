using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>
/// PTR reverse lookups (rung 2 of the naming fallback), made through OPNsense so they're logged as the
/// router's own "localhost" rather than inflating the DNS activity of the machine running Taproom. Lookups
/// are throttled (the router runs each as a backend command), single-flight per address, and cached hard:
/// a definitive "no such name" for an hour, a found name for the caller's TTL, a failure for five minutes.
/// </summary>
public sealed class PtrResolver
{
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan ErrorTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(5);
    private const int MaxConcurrentBatchLookups = 4;
    private const int MaxConcurrentIdentityLookups = 2;

    private readonly IReverseLookupClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PtrResolver> _logger;
    // Separate slots so the few lookups a device page is actively waiting on never queue behind a
    // background batch of remote-address lookups.
    private readonly SemaphoreSlim _batchThrottle = new(MaxConcurrentBatchLookups);
    private readonly SemaphoreSlim _identityThrottle = new(MaxConcurrentIdentityLookups);
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _inFlight = new();
    private long _lastWarningTicks;

    public PtrResolver(IReverseLookupClient client, IMemoryCache cache, ILogger<PtrResolver> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Names for a batch of remote addresses. Only genuinely public unicast addresses are looked up. Waits
    /// up to <paramref name="budget"/> for uncached lookups; any still running keep going in the background
    /// and are served from cache on the next refresh, so a cold page load never waits on a long queue.
    /// </summary>
    public async Task<Dictionary<string, string?>> ResolveManyAsync(
        IEnumerable<string> ips, TimeSpan budget, CancellationToken cancellationToken)
    {
        var lookups = ips
            .Where(PrivateIp.IsPubliclyRoutableUnicast)
            .Distinct()
            .Select(ip => (ip, task: LookupAsync($"ptr:{ip}", ip, TimeSpan.FromHours(1), _batchThrottle)))
            .ToList();

        await Task.WhenAny(Task.WhenAll(lookups.Select(l => l.task)), Task.Delay(budget, cancellationToken));

        return lookups
            .Where(l => l.task.IsCompletedSuccessfully)
            .ToDictionary(l => l.ip, l => l.task.Result);
    }

    /// <summary>
    /// PTR lookup for identifying which name Unbound's log might use for one of THIS client's own addresses
    /// (its "client" field is sometimes an IP, sometimes a resolved name). These are LAN addresses, which is
    /// fine now that the router makes the query. A found name is cached for only 10 minutes since it needs to
    /// track the client's current identity.
    /// </summary>
    public Task<string?> ResolveClientNameAsync(string ip, CancellationToken cancellationToken) =>
        LookupAsync($"client-ptr:{ip}", ip, TimeSpan.FromMinutes(10), _identityThrottle).WaitAsync(cancellationToken);

    private Task<string?> LookupAsync(string cacheKey, string ip, TimeSpan foundTtl, SemaphoreSlim throttle)
    {
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return Task.FromResult(cached);
        }

        // Shared and not tied to any caller's token: callers may stop waiting, the lookup still completes and caches.
        return _inFlight.GetOrAdd(cacheKey, _ => new Lazy<Task<string?>>(() => RunLookupAsync(cacheKey, ip, foundTtl, throttle))).Value;
    }

    private async Task<string?> RunLookupAsync(string cacheKey, string ip, TimeSpan foundTtl, SemaphoreSlim throttle)
    {
        try
        {
            await throttle.WaitAsync();
            PtrLookupOutcome outcome;
            try
            {
                using var cts = new CancellationTokenSource(LookupTimeout);
                outcome = await _client.LookupPtrAsync(ip, cts.Token);
            }
            finally
            {
                throttle.Release();
            }

            var name = outcome.Kind == PtrLookupKind.Found ? outcome.Name : null;
            var ttl = outcome.Kind switch
            {
                PtrLookupKind.Found => foundTtl,
                PtrLookupKind.NoName => NegativeTtl,
                _ => ErrorTtl,
            };
            _cache.Set(cacheKey, name, ttl);
            return name;
        }
        catch (Exception ex)
        {
            WarnOncePerMinute(ex);
            _cache.Set(cacheKey, (string?)null, ErrorTtl);
            return null;
        }
        finally
        {
            _inFlight.TryRemove(cacheKey, out _);
        }
    }

    private void WarnOncePerMinute(Exception ex)
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastWarningTicks);
        if (now - last < TimeSpan.FromMinutes(1).Ticks || Interlocked.CompareExchange(ref _lastWarningTicks, now, last) != last)
        {
            return;
        }
        _logger.LogWarning(ex, "Reverse lookup through OPNsense failed (needs the 'Interfaces: Diagnostics: DNS Lookup' privilege on the API user); falling back to other names");
    }
}
