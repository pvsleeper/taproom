using System.Net.Http.Json;
using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public sealed class UnboundLogClient : IUnboundLogClient
{
    private const int PageSize = 1000;
    private const int MaxPages = 10;

    private readonly HttpClient _http;

    public UnboundLogClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<DnsQueryEntry>> GetQueriesForClientAsync(
        string clientIp, TimeSpan window, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - window;
        var entries = new List<DnsQueryEntry>();

        // Rows come back newest-first, so we can stop paging as soon as a page's rows fall before the cutoff.
        for (var page = 1; page <= MaxPages; page++)
        {
            var pageNumber = page;
            var result = await TransientHttpRetry.RunAsync(async () =>
            {
                var body = new { current = pageNumber, rowCount = PageSize, searchPhrase = clientIp };
                var response = await _http.PostAsJsonAsync("/api/unbound/overview/search_queries", body, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<OpnsenseSearchResult<OpnsenseUnboundQueryDto>>(cancellationToken);
            }, cancellationToken);
            var rows = result?.Rows ?? [];
            if (rows.Count == 0) break;

            var reachedCutoff = false;
            foreach (var row in rows)
            {
                if (!string.Equals(row.Client, clientIp, StringComparison.Ordinal)) continue;
                if (row.Domain is null) continue;

                var time = DateTimeOffset.FromUnixTimeSeconds(row.Time);
                if (time < cutoff)
                {
                    reachedCutoff = true;
                    break;
                }

                entries.Add(new DnsQueryEntry
                {
                    Time = time,
                    Domain = row.Domain.TrimEnd('.'),
                    Type = row.Type ?? "",
                    Blocked = !string.Equals(row.Action, "Pass", StringComparison.OrdinalIgnoreCase),
                    Blocklist = string.IsNullOrEmpty(row.Blocklist) ? null : row.Blocklist,
                    Rcode = row.Rcode ?? "",
                });
            }

            if (reachedCutoff || rows.Count < PageSize) break;
        }

        return entries;
    }
}
