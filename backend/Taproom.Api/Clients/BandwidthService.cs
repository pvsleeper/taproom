using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>Orchestrates a client's live bandwidth: one shared InterfaceSampler sample, summed across the client's own addresses.</summary>
public sealed class BandwidthService
{
    private readonly InterfaceSampler _sampler;
    private readonly IOpnsenseClient _opnsense;
    private readonly ClientSnapshotProvider _snapshotProvider;

    public BandwidthService(InterfaceSampler sampler, IOpnsenseClient opnsense, ClientSnapshotProvider snapshotProvider)
    {
        _sampler = sampler;
        _opnsense = opnsense;
        _snapshotProvider = snapshotProvider;
    }

    public async Task<BandwidthResult?> GetBandwidthAsync(string mac, CancellationToken cancellationToken)
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);
        var client = snapshot.Clients.FirstOrDefault(c => c.Mac == mac);
        if (client is null)
        {
            return null;
        }

        if (client.Ip is null)
        {
            return new BandwidthResult
            {
                Mac = mac,
                SampledAt = DateTimeOffset.UtcNow,
                DownBps = 0,
                UpBps = 0,
                Addresses = [],
                Source = new SourceStatus { Ok = true },
            };
        }

        var addresses = await ClientAddresses.GetAsync(_opnsense, mac, client.Ip, cancellationToken);
        var (records, ok) = await _sampler.GetSampleAsync(cancellationToken);
        var (down, up) = BandwidthCalculator.Sum(records, addresses.ToHashSet());

        return new BandwidthResult
        {
            Mac = mac,
            SampledAt = DateTimeOffset.UtcNow,
            DownBps = down,
            UpBps = up,
            Addresses = addresses,
            Source = new SourceStatus { Ok = ok },
        };
    }
}
