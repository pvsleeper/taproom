using System.Net;
using System.Net.Http.Json;
using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

/// <summary>
/// Reads Unbound's resolver cache via OPNsense's dumpcache diagnostic. The ~1.7 MB response is corrupted
/// by OPNsense over HTTP/1.x (bytes go missing at 64 KB boundaries — reproduced with a raw TLS socket, so
/// it's server-side, not .NET), but arrives intact over HTTP/2 (5 of 5 parsed as valid JSON), so this
/// request insists on HTTP/2.
/// </summary>
public sealed class UnboundCacheClient : IUnboundCacheClient
{
    private readonly HttpClient _http;

    public UnboundCacheClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<DnsCacheRecord>> GetRecordsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/unbound/diagnostics/dumpcache")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var dump = await response.Content.ReadFromJsonAsync<OpnsenseUnboundCacheDump>(cancellationToken)
            ?? throw new InvalidOperationException("Unbound cache dump was empty.");

        return Map(dump);
    }

    public static IReadOnlyList<DnsCacheRecord> Map(OpnsenseUnboundCacheDump dump)
    {
        var records = new List<DnsCacheRecord>(dump.Data.Count);
        foreach (var row in dump.Data)
        {
            if (row.Host is null || row.Value is null || row.RrType is null) continue;
            if (row.RrType is not ("A" or "AAAA" or "CNAME")) continue;

            records.Add(new DnsCacheRecord
            {
                Host = row.Host,
                RrType = row.RrType,
                Value = row.Value,
                TtlSeconds = int.TryParse(row.Ttl, out var ttl) ? ttl : 0,
            });
        }
        return records;
    }
}
