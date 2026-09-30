using System.Net.Http.Json;

namespace Taproom.Api.Sources.Opnsense;

public sealed class InterfaceCounterClient : IInterfaceCounterClient
{
    private readonly HttpClient _http;

    public InterfaceCounterClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<(long BytesReceived, long BytesTransmitted)> GetCountersAsync(
        string interfaceName, CancellationToken cancellationToken)
    {
        var result = await TransientHttpRetry.RunAsync(async () =>
        {
            var response = await _http.GetAsync("/api/diagnostics/traffic/interface", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OpnsenseInterfaceTrafficResult>(cancellationToken);
        }, cancellationToken);

        if (result is null || !result.Interfaces.TryGetValue(interfaceName, out var counters))
        {
            throw new InvalidOperationException($"Interface '{interfaceName}' not found in traffic/interface response.");
        }

        return (
            long.TryParse(counters.BytesReceived, out var rx) ? rx : 0,
            long.TryParse(counters.BytesTransmitted, out var tx) ? tx : 0
        );
    }
}
