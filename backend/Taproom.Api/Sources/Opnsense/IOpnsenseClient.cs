using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface IOpnsenseClient
{
    Task<IReadOnlyList<ArpEntry>> GetArpTableAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DhcpLease>> GetDhcpLeasesAsync(CancellationToken cancellationToken);
}
