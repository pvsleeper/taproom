using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface IUnboundLogClient
{
    Task<IReadOnlyList<DnsQueryEntry>> GetQueriesForClientAsync(ClientMatchSet client, TimeSpan window, CancellationToken cancellationToken);
}
