using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface IUnboundLogClient
{
    Task<IReadOnlyList<DnsQueryEntry>> GetQueriesForClientAsync(ClientMatchSet client, TimeSpan window, CancellationToken cancellationToken);

    /// <summary>Every query network-wide within the window (no client filter), capped at maxRows, for cross-device naming and the dashboard's DNS summary tile.</summary>
    Task<IReadOnlyList<DnsQueryEntry>> GetAllQueriesAsync(TimeSpan window, int maxRows, CancellationToken cancellationToken);
}
