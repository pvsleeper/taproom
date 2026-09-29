using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

public sealed record ClientMatchSet
{
    /// <summary>Every normalized value (IPs and resolved names) that could appear in the Unbound log's "client" field for this device.</summary>
    public required IReadOnlySet<string> Names { get; init; }

    /// <summary>
    /// Values to send as the search endpoint's searchPhrase, one query per entry. There's no way to know
    /// in advance which literal form (IP vs. a resolved name) the log will actually use for this device —
    /// it varies per device — so every candidate is queried and results are merged, rather than guessing one.
    /// </summary>
    public required IReadOnlyList<string> SearchPhrases { get; init; }
}

/// <summary>
/// Figures out every name OPNsense's Unbound log might use for one client, since its "client" field is
/// sometimes the IP and sometimes a resolved name depending on what OPNsense has cached.
/// </summary>
public sealed class ClientIdentityResolver
{
    private readonly IOpnsenseClient _opnsense;
    private readonly DomainResolver _domainResolver;

    public ClientIdentityResolver(IOpnsenseClient opnsense, DomainResolver domainResolver)
    {
        _opnsense = opnsense;
        _domainResolver = domainResolver;
    }

    public async Task<ClientMatchSet> ResolveAsync(string mac, string ipv4, CancellationToken cancellationToken)
    {
        var ndp = await _opnsense.GetNdpTableAsync(cancellationToken);
        var ipv6Addresses = ndp.Where(n => n.Mac == mac).Select(n => n.Ipv6).ToList();

        var addresses = new List<string> { ipv4 };
        addresses.AddRange(ipv6Addresses);

        var names = new HashSet<string>();
        var searchPhrases = new List<string>();

        var ptrResults = await Task.WhenAll(addresses.Select(async addr => (addr, ptr: await _domainResolver.ResolveClientNameAsync(addr, cancellationToken))));

        foreach (var addr in addresses)
        {
            var normalizedAddr = ClientMatching.Normalize(addr);
            names.Add(normalizedAddr);
        }
        // The IPv4 address is always a candidate — plenty of devices show up in the log by plain IP.
        searchPhrases.Add(ClientMatching.Normalize(ipv4));

        foreach (var (_, ptr) in ptrResults)
        {
            if (ptr is null) continue;
            var normalized = ClientMatching.Normalize(ptr);
            names.Add(normalized);
            if (!searchPhrases.Contains(normalized))
            {
                searchPhrases.Add(normalized);
            }
        }

        return new ClientMatchSet { Names = names, SearchPhrases = searchPhrases };
    }
}
