using System.Text.Json.Serialization;

namespace Taproom.Api.Sources.Opnsense;

// Confirmed against a live router (OPNsense, dnsmasq DHCP backend) — see docs/Taproom — Phase 1 Spec.md.

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
