using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Taproom.Api.Clients;
using Taproom.Api.Config;

namespace Taproom.Api.Sources.Opnsense;

public sealed class OpnsenseClient : IOpnsenseClient
{
    private readonly HttpClient _http;
    private readonly OpnsenseOptions _options;

    public OpnsenseClient(HttpClient http, IOptions<TaproomOptions> options)
    {
        _http = http;
        _options = options.Value.Opnsense;
    }

    public async Task<IReadOnlyList<ArpEntry>> GetArpTableAsync(CancellationToken cancellationToken)
    {
        var rows = await _http.GetFromJsonAsync<List<OpnsenseArpEntryDto>>(
            "/api/diagnostics/interface/get_arp", cancellationToken) ?? [];

        var now = DateTimeOffset.UtcNow;
        var result = new List<ArpEntry>(rows.Count);
        foreach (var row in rows)
        {
            var mac = MacAddress.Normalize(row.Mac);
            if (mac is null) continue;
            if (row.Expired) continue;
            // The ARP table also carries the WAN-side gateway/neighbors, which aren't LAN clients.
            if (string.Equals(row.IntfDescription, "WAN", StringComparison.OrdinalIgnoreCase)) continue;

            result.Add(new ArpEntry
            {
                Mac = mac,
                Ip = row.Ip,
                Hostname = NullIfUnknown(row.Hostname),
                ObservedAt = now,
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<DhcpLease>> GetDhcpLeasesAsync(CancellationToken cancellationToken)
    {
        return _options.DhcpProvider switch
        {
            DhcpProvider.Dnsmasq => await GetDnsmasqLeasesAsync(cancellationToken),
            DhcpProvider.Kea => await GetKeaLeasesAsync(cancellationToken),
            DhcpProvider.Isc => await GetIscLeasesAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unknown DHCP provider: {_options.DhcpProvider}"),
        };
    }

    private async Task<IReadOnlyList<DhcpLease>> GetDnsmasqLeasesAsync(CancellationToken cancellationToken)
    {
        var result = await _http.GetFromJsonAsync<OpnsenseSearchResult<DnsmasqLeaseDto>>(
            "/api/dnsmasq/leases/search", cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var leases = new List<DhcpLease>();
        foreach (var row in result?.Rows ?? [])
        {
            var mac = MacAddress.Normalize(row.Hwaddr);
            if (mac is null) continue;
            // dnsmasq lists both the IPv4 and IPv6 leases for a dual-stack client; phase 1 only shows IPv4.
            if (row.Address is null || row.Address.Contains(':')) continue;

            leases.Add(new DhcpLease
            {
                Mac = mac,
                Ip = row.Address,
                Hostname = NullIfUnknown(row.Hostname),
                IsStatic = row.IsReserved.Count > 0,
                ObservedAt = now,
            });
        }

        return leases;
    }

    private async Task<IReadOnlyList<DhcpLease>> GetKeaLeasesAsync(CancellationToken cancellationToken)
    {
        var result = await _http.GetFromJsonAsync<OpnsenseSearchResult<KeaLeaseDto>>(
            "/api/kea/leases4/search", cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var leases = new List<DhcpLease>();
        foreach (var row in result?.Rows ?? [])
        {
            var mac = MacAddress.Normalize(row.HwAddress);
            if (mac is null) continue;

            leases.Add(new DhcpLease
            {
                Mac = mac,
                Ip = row.IpAddress,
                Hostname = NullIfUnknown(row.Hostname),
                IsStatic = string.Equals(row.State, "reserved", StringComparison.OrdinalIgnoreCase),
                ObservedAt = now,
            });
        }

        return leases;
    }

    private async Task<IReadOnlyList<DhcpLease>> GetIscLeasesAsync(CancellationToken cancellationToken)
    {
        var result = await _http.GetFromJsonAsync<OpnsenseSearchResult<IscLeaseDto>>(
            "/api/dhcpv4/leases/search_lease", cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var leases = new List<DhcpLease>();
        foreach (var row in result?.Rows ?? [])
        {
            var mac = MacAddress.Normalize(row.Mac);
            if (mac is null) continue;

            leases.Add(new DhcpLease
            {
                Mac = mac,
                Ip = row.Address,
                Hostname = NullIfUnknown(row.Hostname),
                IsStatic = row.IsReserved == "1",
                ObservedAt = now,
            });
        }

        return leases;
    }

    private static string? NullIfUnknown(string? hostname) =>
        string.IsNullOrWhiteSpace(hostname) || hostname is "?" or "*" ? null : hostname;
}
