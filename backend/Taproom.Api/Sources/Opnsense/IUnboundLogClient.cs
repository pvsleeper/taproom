using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface IUnboundLogClient
{
    Task<IReadOnlyList<DnsQueryEntry>> GetQueriesForClientAsync(string clientIp, TimeSpan window, CancellationToken cancellationToken);
}
