using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface IStatesClient
{
    Task<IReadOnlyList<FirewallState>> GetStatesForClientAsync(IReadOnlyList<string> clientAddresses, CancellationToken cancellationToken);
}
