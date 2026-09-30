using System.Text.Json.Serialization;

namespace Taproom.Api.Sources.Opnsense;

// Confirmed against a live router (OPNsense, dnsmasq DHCP backend) — see docs/Taproom — Phase 1 Spec.md.

/// <summary>
/// GET /api/diagnostics/traffic/interface returns { "interfaces": { "&lt;name&gt;": {...} } } with
/// cumulative, ever-increasing byte counters as strings (not a rate — the caller computes deltas).
/// Confirmed against a live router with a real download: on the WAN interface, "bytes received" is the
/// house's download and "bytes transmitted" is its upload (opposite of phase 4's per-address LAN mapping,
/// as the phase 5 spec warned).
/// </summary>
public sealed class OpnsenseInterfaceTrafficResult
{
    [JsonPropertyName("interfaces")]
    public Dictionary<string, OpnsenseInterfaceCountersDto> Interfaces { get; set; } = new();
}

public sealed class OpnsenseInterfaceCountersDto
{
    [JsonPropertyName("bytes received")]
    public string? BytesReceived { get; set; }

    [JsonPropertyName("bytes transmitted")]
    public string? BytesTransmitted { get; set; }
}

/// <summary>
/// GET /api/diagnostics/traffic/top/{interface} returns { "&lt;interface&gt;": { "records": [...] } }.
/// Confirmed against a live router: rate_bits_in/out are already instantaneous bits-per-second (not a
/// cumulative count over the sample), and — counter to the phase 4 spec's assumption — "in" is a live
/// download to that address and "out" is its upload (confirmed by watching real download/upload traffic;
/// the field is labeled from the host's own perspective, not the interface's routing direction).
/// </summary>
public sealed class OpnsenseTrafficTopResult
{
    [JsonPropertyName("records")]
    public List<OpnsenseTopTalkerDto> Records { get; set; } = new();
}

public sealed class OpnsenseTopTalkerDto
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("rate_bits_in")]
    public long RateBitsIn { get; set; }

    [JsonPropertyName("rate_bits_out")]
    public long RateBitsOut { get; set; }
}

/// <summary>One row from GET /api/diagnostics/interface/get_arp.</summary>
public sealed class OpnsenseArpEntryDto
{
    [JsonPropertyName("mac")]
    public string? Mac { get; set; }

    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("intf_description")]
    public string? IntfDescription { get; set; }

    [JsonPropertyName("expired")]
    public bool Expired { get; set; }
}

/// <summary>
/// One row from GET /api/diagnostics/interface/get_ndp. Field names mirror get_arp's shape (OPNsense's
/// IPv4 neighbor table) since this is its IPv6 analog, but haven't been confirmed against a live
/// response — the taproom API user didn't have this page's privilege yet when this was written.
/// </summary>
public sealed class OpnsenseNdpEntryDto
{
    [JsonPropertyName("mac")]
    public string? Mac { get; set; }

    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("intf_description")]
    public string? IntfDescription { get; set; }

    [JsonPropertyName("expired")]
    public bool Expired { get; set; }
}

/// <summary>Common "rows" envelope used by OPNsense's search-grid endpoints.</summary>
public sealed class OpnsenseSearchResult<T>
{
    [JsonPropertyName("rows")]
    public List<T> Rows { get; set; } = new();

    [JsonPropertyName("rowCount")]
    public int RowCount { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>One row from GET /api/dnsmasq/leases/search.</summary>
public sealed class DnsmasqLeaseDto
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("hwaddr")]
    public string? Hwaddr { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    /// <summary>Non-empty when the lease is pinned to a static mapping, e.g. ["hwaddr"].</summary>
    [JsonPropertyName("is_reserved")]
    public List<string> IsReserved { get; set; } = new();
}

/// <summary>One row from GET /api/kea/leases4/search.</summary>
public sealed class KeaLeaseDto
{
    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("hw_address")]
    public string? HwAddress { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

/// <summary>One row from GET /api/dhcpv4/leases/search_lease.</summary>
public sealed class IscLeaseDto
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("mac")]
    public string? Mac { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("binding_state")]
    public string? BindingState { get; set; }

    [JsonPropertyName("is_reserved")]
    public string? IsReserved { get; set; }
}

/// <summary>
/// One row from POST /api/diagnostics/firewall/query_states. Each real connection appears twice — once
/// on the "LAN to any" rule with the client's real LAN IP as src_addr (pre-NAT), and once on an internal
/// NAT/outbound rule with the router's WAN IP as src_addr (post-NAT). Filtering src_addr == the client's
/// exact IP keeps only the pre-NAT row and naturally drops the duplicate.
/// </summary>
public sealed class OpnsenseStateDto
{
    [JsonPropertyName("proto")]
    public string? Proto { get; set; }

    [JsonPropertyName("src_addr")]
    public string? SrcAddr { get; set; }

    [JsonPropertyName("src_port")]
    public string? SrcPort { get; set; }

    [JsonPropertyName("dst_addr")]
    public string? DstAddr { get; set; }

    [JsonPropertyName("dst_port")]
    public string? DstPort { get; set; }

    /// <summary>e.g. "ESTABLISHED:ESTABLISHED" (client-to-server : server-to-client).</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>Formatted duration, e.g. "06:47:00".</summary>
    [JsonPropertyName("age")]
    public string? Age { get; set; }

    /// <summary>[in, out].</summary>
    [JsonPropertyName("pkts")]
    public long[]? Packets { get; set; }

    /// <summary>[in, out].</summary>
    [JsonPropertyName("bytes")]
    public long[]? Bytes { get; set; }
}

/// <summary>One row from POST /api/unbound/overview/search_queries.</summary>
public sealed class OpnsenseUnboundQueryDto
{
    /// <summary>Unix seconds.</summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("client")]
    public string? Client { get; set; }

    /// <summary>DNS record type queried, e.g. "A", "AAAA", "HTTPS".</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Trailing-dot FQDN, e.g. "youtube.com.".</summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    /// <summary>"Pass" when allowed; anything else (e.g. "Block") is treated as blocked.</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("blocklist")]
    public string? Blocklist { get; set; }

    [JsonPropertyName("rcode")]
    public string? Rcode { get; set; }
}
