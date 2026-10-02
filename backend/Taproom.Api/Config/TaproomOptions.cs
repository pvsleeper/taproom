namespace Taproom.Api.Config;

public sealed class TaproomOptions
{
    public const string SectionName = "Taproom";

    public int CacheSeconds { get; set; } = 30;
    public OmadaOptions Omada { get; set; } = new();
    public OpnsenseOptions Opnsense { get; set; } = new();
    public HomeOptions Home { get; set; } = new();
    public GeoIpOptions GeoIp { get; set; } = new();
}

public sealed class HomeOptions
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Label { get; set; } = "Home";
}

public sealed class GeoIpOptions
{
    public string CityDbPath { get; set; } = "";
    public string AsnDbPath { get; set; } = "";
}

public sealed class OmadaOptions
{
    public string BaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string SiteName { get; set; } = "Default";
    public bool AllowInvalidCertificate { get; set; }
}

public enum DhcpProvider
{
    Dnsmasq,
    Kea,
    Isc,
}

public sealed class OpnsenseOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public bool AllowInvalidCertificate { get; set; }
    public DhcpProvider DhcpProvider { get; set; } = DhcpProvider.Dnsmasq;
    /// <summary>Logical interface name for the top-talkers bandwidth sample, e.g. "lan".</summary>
    public string LanInterface { get; set; } = "lan";
    /// <summary>Logical interface name for the WAN bandwidth counters, e.g. "wan".</summary>
    public string WanInterface { get; set; } = "wan";
}
