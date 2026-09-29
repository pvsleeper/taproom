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
        ClientMatchSet client, TimeSpan window, CancellationToken cancellationToken)
    {
        // We can't know in advance which literal value (an IP or a resolved name) the log uses for this
        // device, so every candidate search phrase is queried and the (deduplicated) results merged.
        var perPhraseResults = await Task.WhenAll(
            client.SearchPhrases.Select(phrase => FetchForPhraseAsync(phrase, client.Names, window, cancellationToken)));

        var seen = new HashSet<(long, string, string)>();
        var merged = new List<DnsQueryEntry>();
        foreach (var entry in perPhraseResults.SelectMany(r => r))
        {
            if (seen.Add((entry.Time.ToUnixTimeSeconds(), entry.Domain, entry.Type)))
            {
                merged.Add(entry);
            }
        }

        return merged;
    }

    private async Task<IReadOnlyList<DnsQueryEntry>> FetchForPhraseAsync(
        string searchPhrase, IReadOnlySet<string> matchNames, TimeSpan window, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - window;
        var entries = new List<DnsQueryEntry>();

        // Rows come back newest-first, so we can stop paging as soon as a page's rows fall before the cutoff.
        for (var page = 1; page <= MaxPages; page++)
        {
            var pageNumber = page;
            var result = await TransientHttpRetry.RunAsync(async () =>
            {
                // searchPhrase does a substring match across all columns (so it also returns other
                // clients' lookups OF this name as a domain) — it's just a server-side prefilter; the
                // exact per-row client check below is what actually scopes results to this device.
                var body = new { current = pageNumber, rowCount = PageSize, searchPhrase };
                var response = await _http.PostAsJsonAsync("/api/unbound/overview/search_queries", body, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<OpnsenseSearchResult<OpnsenseUnboundQueryDto>>(cancellationToken);
            }, cancellationToken);
            var rows = result?.Rows ?? [];
            if (rows.Count == 0) break;

            var reachedCutoff = false;
            foreach (var row in rows)
            {
                if (!ClientMatching.Matches(row.Client, matchNames)) continue;
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
                    Blocked = DnsQueryClassifier.IsBlocked(row.Action, row.Blocklist),
                    Failed = DnsQueryClassifier.IsFailed(row.Action, row.Blocklist, row.Rcode),
                    Blocklist = string.IsNullOrEmpty(row.Blocklist) ? null : row.Blocklist,
                    Rcode = row.Rcode ?? "",
                });
            }

            if (reachedCutoff || rows.Count < PageSize) break;
        }

        return entries;
    }
}
