using System.Text.Json;
using Taproom.Api.Clients;

namespace Taproom.Tests;

public class ClientMergerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly IReadOnlyList<OmadaClientRecord> OmadaClients =
        LoadFixture<OmadaClientRecord>("omada-clients.json");

    private static readonly IReadOnlyList<ArpEntry> ArpEntries =
        LoadFixture<ArpEntry>("arp-entries.json");

    private static readonly IReadOnlyList<DhcpLease> DhcpLeases =
        LoadFixture<DhcpLease>("dhcp-leases.json");

    private static List<T> LoadFixture<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Fixture '{fileName}' deserialized to null.");
    }

    private static NetworkClient Merge(string mac) =>
        ClientMerger.Merge(OmadaClients, ArpEntries, DhcpLeases).Single(c => c.Mac == mac);

    [Fact]
    public void Merges_client_seen_by_both_omada_and_arp_into_one_record()
    {
        var all = ClientMerger.Merge(OmadaClients, ArpEntries, DhcpLeases);
        Assert.Equal(1, all.Count(c => c.Mac == "AA:BB:CC:DD:EE:01"));
    }

    [Fact]
    public void Normalizes_mac_regardless_of_source_separator_style()
    {
        // omada uses "aa:bb:cc:dd:ee:01", arp uses "AA-BB-CC-DD-EE-01" for the same device.
        var client = Merge("AA:BB:CC:DD:EE:01");
        Assert.Equal("AA:BB:CC:DD:EE:01", client.Mac);
    }

    [Fact]
    public void Prefers_omada_custom_name_over_other_sources()
    {
        var client = Merge("AA:BB:CC:DD:EE:01");
        Assert.Equal("Pieter-iPhone", client.Name);
    }

    [Fact]
    public void Falls_back_to_omada_hostname_when_no_custom_name()
    {
        var client = Merge("AA:BB:CC:DD:EE:02");
        Assert.Equal("living-room-tv", client.Name);
    }

    [Fact]
    public void Prefers_dhcp_hostname_over_arp_hostname_when_no_omada_data()
    {
        var client = Merge("AA:BB:CC:DD:EE:03");
        Assert.Equal("office-desktop-dhcp", client.Name);
        // No Omada record, but it IS currently online via ARP — Omada would have caught it if wireless,
        // so a live, non-Omada-tracked client is confidently Wired, not Unknown.
        Assert.Equal(ConnectionType.Wired, client.Connection);
    }

    [Fact]
    public void Prefers_arp_ip_over_omada_ip()
    {
        // omada fixture doesn't set ip for this record's counterpart; arp is authoritative when present.
        var client = Merge("AA:BB:CC:DD:EE:01");
        Assert.Equal("10.0.1.52", client.Ip);
    }

    [Fact]
    public void Client_with_no_omada_data_is_wired()
    {
        var client = Merge("AA:BB:CC:DD:EE:03");
        Assert.Equal(ConnectionType.Wired, client.Connection);
        Assert.Null(client.Ssid);
        Assert.Null(client.ApName);
    }

    [Fact]
    public void Client_with_omada_wireless_flag_is_wireless()
    {
        var client = Merge("AA:BB:CC:DD:EE:01");
        Assert.Equal(ConnectionType.Wireless, client.Connection);
        Assert.Equal("venter", client.Ssid);
        Assert.Equal("EAP670 Downstairs", client.ApName);
    }

    [Fact]
    public void Dhcp_only_client_with_no_arp_or_omada_is_offline_but_included()
    {
        var client = Merge("AA:BB:CC:DD:EE:04");
        Assert.False(client.Online);
        Assert.Equal("unplugged-nas", client.Name);
        Assert.True(client.IsStaticLease);
        Assert.Equal(["dhcp"], client.Sources);
    }

    [Fact]
    public void Offline_dhcp_only_client_has_unknown_connection_type()
    {
        // Omada has no history for it and it's not live in ARP, so we genuinely don't know if it's wired or wireless.
        var client = Merge("AA:BB:CC:DD:EE:04");
        Assert.Equal(ConnectionType.Unknown, client.Connection);
    }

    [Fact]
    public void Client_present_in_arp_is_online_even_without_omada()
    {
        var client = Merge("AA:BB:CC:DD:EE:03");
        Assert.True(client.Online);
    }

    [Fact]
    public void Sources_list_reflects_which_inputs_contributed()
    {
        var client = Merge("AA:BB:CC:DD:EE:01");
        Assert.Contains("omada", client.Sources);
        Assert.Contains("arp", client.Sources);
    }

    [Fact]
    public void Merge_produces_one_record_per_unique_mac()
    {
        var all = ClientMerger.Merge(OmadaClients, ArpEntries, DhcpLeases);
        Assert.Equal(4, all.Count);
    }
}
