using System.Text.Json.Serialization;

namespace Taproom.Api.Sources.Omada;

// Omada Open API wraps every response the same way. Field names below follow the documented
// Open API v5 shape; Claude Code could not reach a live controller to confirm them against its
// Swagger docs, so verify each against the real controller before relying on this in production.

public sealed class OmadaEnvelope<T>
{
    [JsonPropertyName("errorCode")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }
}

public sealed class OmadaInfoResult
{
    [JsonPropertyName("omadacId")]
    public string OmadacId { get; set; } = "";
}

public sealed class OmadaTokenRequest
{
    [JsonPropertyName("omadacId")]
    public string OmadacId { get; set; } = "";

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "";

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; } = "";
}

public sealed class OmadaTokenResult
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("tokenType")]
    public string TokenType { get; set; } = "";

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; }
}

public sealed class OmadaPagedResult<T>
{
    [JsonPropertyName("totalRows")]
    public int TotalRows { get; set; }

    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();
}

public sealed class OmadaSiteDto
{
    [JsonPropertyName("siteId")]
    public string SiteId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class OmadaClientDto
{
    [JsonPropertyName("mac")]
    public string Mac { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hostName")]
    public string? HostName { get; set; }

    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("wireless")]
    public bool Wireless { get; set; }

    [JsonPropertyName("ssid")]
    public string? Ssid { get; set; }

    [JsonPropertyName("apMac")]
    public string? ApMac { get; set; }

    /// <summary>0 = 2.4 GHz, 1 = 5 GHz, 2 = 6 GHz.</summary>
    [JsonPropertyName("radioId")]
    public int? RadioId { get; set; }

    [JsonPropertyName("rssi")]
    public int? Rssi { get; set; }

    [JsonPropertyName("trafficDown")]
    public long? TrafficDown { get; set; }

    [JsonPropertyName("trafficUp")]
    public long? TrafficUp { get; set; }

    /// <summary>Seconds since the client connected.</summary>
    [JsonPropertyName("uptime")]
    public long? Uptime { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>Epoch millis of the last time this client was seen.</summary>
    [JsonPropertyName("lastSeen")]
    public long? LastSeen { get; set; }
}

public sealed class OmadaDeviceDto
{
    [JsonPropertyName("mac")]
    public string Mac { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
