using System.Net.Http.Json;
using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public sealed class TopTalkersClient : ITopTalkersClient
{
    private readonly HttpClient _http;

    public TopTalkersClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<TopTalkerRecord>> GetTopTalkersAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var result = await TransientHttpRetry.RunAsync(async () =>
        {
            var response = await _http.GetAsync($"/api/diagnostics/traffic/top/{interfaceName}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Dictionary<string, OpnsenseTrafficTopResult>>(cancellationToken);
        }, cancellationToken);

        if (result is null || !result.TryGetValue(interfaceName, out var forInterface))
        {
            return [];
        }

        return BandwidthCalculator.MapRecords(forInterface);
    }
}
