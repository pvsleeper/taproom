using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface IStatesClient
{
    Task<IReadOnlyList<FirewallState>> GetStatesForClientAsync(IReadOnlyList<string> clientAddresses, CancellationToken cancellationToken);

    /// <summary>Every LAN-originated state network-wide (no client filter), with SrcAddr populated for device attribution.</summary>
    Task<IReadOnlyList<FirewallState>> GetAllStatesAsync(CancellationToken cancellationToken);
}
