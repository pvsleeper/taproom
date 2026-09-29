using System.Net.Http.Json;
using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public sealed class StatesClient : IStatesClient
{
    private readonly HttpClient _http;

    public StatesClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Queries once per address (IPv4 and any IPv6) and merges results, so a dual-stack client's IPv6-sourced connections aren't missed.</summary>
    public async Task<IReadOnlyList<FirewallState>> GetStatesForClientAsync(
        IReadOnlyList<string> clientAddresses, CancellationToken cancellationToken)
    {
        var perAddress = await Task.WhenAll(
            clientAddresses.Select(address => TransientHttpRetry.RunAsync(() => FetchAsync(address, cancellationToken), cancellationToken)));
        return perAddress.SelectMany(states => states).ToList();
    }

    private async Task<IReadOnlyList<FirewallState>> FetchAsync(string address, CancellationToken cancellationToken)
    {
        var body = new { current = 1, rowCount = 500, searchPhrase = address };
        var response = await _http.PostAsJsonAsync("/api/diagnostics/firewall/query_states", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpnsenseSearchResult<OpnsenseStateDto>>(cancellationToken);

        var states = new List<FirewallState>();
        foreach (var row in result?.Rows ?? [])
        {
            // Each real connection appears twice (pre-NAT and post-NAT); keep only the row where this
            // client's own address is literally the source, which is always the pre-NAT one.
            if (!string.Equals(row.SrcAddr, address, StringComparison.Ordinal)) continue;
            if (row.DstAddr is null || !int.TryParse(row.DstPort, out var remotePort)) continue;

            // bytes/pkts are [out, in] relative to this client (confirmed against a live download: the
            // second element is what grows while downloading).
            var bytes = row.Bytes ?? [];
            var upBytes = bytes.Length > 0 ? bytes[0] : 0;
            var downBytes = bytes.Length > 1 ? bytes[1] : 0;

            states.Add(new FirewallState
            {
                Protocol = row.Proto ?? "unknown",
                RemoteIp = row.DstAddr,
                RemotePort = remotePort,
                State = row.State?.Split(':')[0] ?? "UNKNOWN",
                DownBytes = downBytes,
                UpBytes = upBytes,
                Packets = Sum(row.Packets),
                AgeSeconds = ParseAgeSeconds(row.Age),
            });
        }

        return states;
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
