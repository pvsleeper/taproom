using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public sealed class StatesClient : IStatesClient
{
    // OPNsense's web backend reliably corrupts the chunked response above roughly 150KB (confirmed by
    // bisecting rowCount live: 200 rows/~100KB succeeds consistently, 300 rows/~150KB fails consistently,
    // even with ConnectionClose set) — a real size threshold, not just load. Small pages + real pagination
    // sidesteps it, unlike the single "rowCount=5000" call the phase 5 spec suggested.
    private const int UnfilteredPageSize = 200;
    private const int MaxUnfilteredPages = 30;

    private readonly HttpClient _http;
    private readonly ILogger<StatesClient> _logger;

    public StatesClient(HttpClient http, ILogger<StatesClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>Queries once per address (IPv4 and any IPv6) and merges results, so a dual-stack client's IPv6-sourced connections aren't missed.</summary>
    public async Task<IReadOnlyList<FirewallState>> GetStatesForClientAsync(
        IReadOnlyList<string> clientAddresses, CancellationToken cancellationToken)
    {
        var perAddress = await Task.WhenAll(
            clientAddresses.Select(address => TransientHttpRetry.RunAsync(() => FetchPageAsync(1, address, cancellationToken), cancellationToken)));
        var rows = perAddress.SelectMany(r => r.Rows).ToList();
        return rows
            .Where(row => clientAddresses.Contains(row.SrcAddr, StringComparer.Ordinal))
            .Select(MapRow)
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();
    }

    public async Task<IReadOnlyList<FirewallState>> GetAllStatesAsync(CancellationToken cancellationToken)
    {
        var first = await TransientHttpRetry.RunAsync(() => FetchPageAsync(1, "", cancellationToken), cancellationToken);
        var allRows = new List<OpnsenseStateDto>(first.Rows);

        var totalPages = (int)Math.Ceiling(first.Total / (double)UnfilteredPageSize);
        var pagesToFetch = Math.Min(totalPages, MaxUnfilteredPages);

        if (totalPages > MaxUnfilteredPages)
        {
            _logger.LogWarning(
                "Unfiltered firewall states total ({Total}) exceeds what we fetch ({Fetched} of {PageSize}-row pages); some connections will be missing from the dashboard.",
                first.Total, MaxUnfilteredPages * UnfilteredPageSize, UnfilteredPageSize);
        }

        if (pagesToFetch > 1)
        {
            var remaining = await Task.WhenAll(Enumerable.Range(2, pagesToFetch - 1)
                .Select(page => TransientHttpRetry.RunAsync(() => FetchPageAsync(page, "", cancellationToken), cancellationToken)));
            allRows.AddRange(remaining.SelectMany(r => r.Rows));
        }

        // Pre-NAT rows have a private LAN address as their own source; post-NAT echo rows have the
        // router's public WAN address instead — filtering to a private src_addr keeps only the former,
        // without needing to know every client's address in advance like the per-client fetch does.
        return allRows
            .Where(row => row.SrcAddr is not null && PrivateIp.IsPrivateOrLinkLocal(row.SrcAddr))
            .Select(MapRow)
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();
    }

    private async Task<(List<OpnsenseStateDto> Rows, int Total)> FetchPageAsync(int page, string searchPhrase, CancellationToken cancellationToken)
    {
        var body = new { current = page, rowCount = UnfilteredPageSize, searchPhrase };
        var response = await _http.PostAsJsonAsync("/api/diagnostics/firewall/query_states", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpnsenseSearchResult<OpnsenseStateDto>>(cancellationToken);
        return (result?.Rows ?? [], result?.Total ?? 0);
    }

    private static FirewallState? MapRow(OpnsenseStateDto row)
    {
        if (row.SrcAddr is null || row.DstAddr is null || !int.TryParse(row.DstPort, out var remotePort))
        {
            return null;
        }

        // bytes/pkts are [out, in] relative to this client (confirmed against a live download: the
        // second element is what grows while downloading).
        var bytes = row.Bytes ?? [];
        var upBytes = bytes.Length > 0 ? bytes[0] : 0;
        var downBytes = bytes.Length > 1 ? bytes[1] : 0;

        return new FirewallState
        {
            SrcAddr = row.SrcAddr,
            Protocol = row.Proto ?? "unknown",
            RemoteIp = row.DstAddr,
            RemotePort = remotePort,
            State = row.State?.Split(':')[0] ?? "UNKNOWN",
            DownBytes = downBytes,
            UpBytes = upBytes,
            Packets = Sum(row.Packets),
            AgeSeconds = ParseAgeSeconds(row.Age),
        };
    }

    private static long Sum(long[]? values) => values is null ? 0 : values.Sum();

    private static int ParseAgeSeconds(string? age)
    {
        if (age is null) return 0;
        var parts = age.Split(':');
        if (parts.Length != 3) return 0;
        if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var s))
        {
            return 0;
        }
        return h * 3600 + m * 60 + s;
    }
}
