using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface IReverseLookupClient
{
    Task<PtrLookupOutcome> LookupPtrAsync(string ip, CancellationToken cancellationToken);
}
