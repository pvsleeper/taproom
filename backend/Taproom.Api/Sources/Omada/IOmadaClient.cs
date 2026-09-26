using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Omada;

public interface IOmadaClient
{
    Task<IReadOnlyList<OmadaClientRecord>> GetClientsAsync(CancellationToken cancellationToken);
}
