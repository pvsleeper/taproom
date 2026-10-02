using System.Net.Http.Json;
using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

/// <summary>
/// Reverse lookups made by the router itself (the endpoint behind Interfaces → Diagnostics → DNS Lookup), so
/// Unbound logs them as client "localhost" instead of as queries from the machine running Taproom. Needs the
/// "Interfaces: Diagnostics: DNS Lookup" privilege on the API key's user. The call validates and runs the
/// lookup without saving anything (the stored settings stay empty).
/// </summary>
public sealed class ReverseLookupClient : IReverseLookupClient
{
    private readonly HttpClient _http;

    public ReverseLookupClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<PtrLookupOutcome> LookupPtrAsync(string ip, CancellationToken cancellationToken)
    {
        var body = new { dns = new { settings = new { hostname = ip, server = "" } } };
        using var response = await _http.PostAsJsonAsync("/api/diagnostics/dns_diagnostics/set", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return PtrAnswerParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
