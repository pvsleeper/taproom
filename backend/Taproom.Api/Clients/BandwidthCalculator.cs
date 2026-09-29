using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

public static class BandwidthCalculator
{
    /// <summary>Maps a raw traffic-top result to normalized records — down = rate_bits_in, up = rate_bits_out
    /// (confirmed against a live router: the field is labeled from the host's own perspective).</summary>
    public static IReadOnlyList<TopTalkerRecord> MapRecords(OpnsenseTrafficTopResult? result)
    {
        if (result is null) return [];

        var records = new List<TopTalkerRecord>(result.Records.Count);
        foreach (var row in result.Records)
        {
            if (row.Address is null) continue;
            records.Add(new TopTalkerRecord { Address = row.Address, DownBps = row.RateBitsIn, UpBps = row.RateBitsOut });
        }
        return records;
    }

    /// <summary>Sums the rates of every record whose address belongs to the given client — its IPv4 plus any IPv6 addresses.</summary>
    public static (long DownBps, long UpBps) Sum(IReadOnlyList<TopTalkerRecord> records, IReadOnlySet<string> clientAddresses)
    {
        long down = 0, up = 0;
        foreach (var record in records)
        {
            if (!clientAddresses.Contains(record.Address)) continue;
            down += record.DownBps;
            up += record.UpBps;
        }
        return (down, up);
    }
}
