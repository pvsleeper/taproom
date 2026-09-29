using Taproom.Api.Clients;

namespace Taproom.Api.Sources.Opnsense;

public interface ITopTalkersClient
{
    Task<IReadOnlyList<TopTalkerRecord>> GetTopTalkersAsync(string interfaceName, CancellationToken cancellationToken);
}
