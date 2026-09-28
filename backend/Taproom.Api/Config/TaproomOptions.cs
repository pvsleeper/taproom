namespace Taproom.Api.Config;

public sealed class TaproomOptions
{
    public const string SectionName = "Taproom";

    public int CacheSeconds { get; set; } = 30;
    public OmadaOptions Omada { get; set; } = new();
    public OpnsenseOptions Opnsense { get; set; } = new();
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
}
