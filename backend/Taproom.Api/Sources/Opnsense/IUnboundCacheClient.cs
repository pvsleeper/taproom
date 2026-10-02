using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface IUnboundCacheClient
{
    Task<IReadOnlyList<DnsCacheRecord>> GetRecordsAsync(CancellationToken cancellationToken);
}
