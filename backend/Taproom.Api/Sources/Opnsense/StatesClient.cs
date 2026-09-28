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

    public Task<IReadOnlyList<FirewallState>> GetStatesForClientAsync(string clientIp, CancellationToken cancellationToken) =>
        TransientHttpRetry.RunAsync(() => FetchAsync(clientIp, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<FirewallState>> FetchAsync(string clientIp, CancellationToken cancellationToken)
    {
        var body = new { current = 1, rowCount = 500, searchPhrase = clientIp };
        var response = await _http.PostAsJsonAsync("/api/diagnostics/firewall/query_states", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpnsenseSearchResult<OpnsenseStateDto>>(cancellationToken);

        var states = new List<FirewallState>();
        foreach (var row in result?.Rows ?? [])
        {
            // Each real connection appears twice (pre-NAT and post-NAT); keep only the row where this
            // client's own IP is literally the source, which is always the pre-NAT one.
            if (!string.Equals(row.SrcAddr, clientIp, StringComparison.Ordinal)) continue;
            if (row.DstAddr is null || !int.TryParse(row.DstPort, out var remotePort)) continue;

            states.Add(new FirewallState
            {
                Protocol = row.Proto ?? "unknown",
                RemoteIp = row.DstAddr,
                RemotePort = remotePort,
                State = row.State?.Split(':')[0] ?? "UNKNOWN",
                Bytes = Sum(row.Bytes),
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
