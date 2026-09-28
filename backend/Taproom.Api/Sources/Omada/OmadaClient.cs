using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Taproom.Api.Clients;
using Taproom.Api.Config;

namespace Taproom.Api.Sources.Omada;

public sealed class OmadaClient : IOmadaClient
{
    private readonly HttpClient _http;
    private readonly OmadaOptions _options;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private string? _omadacId;
    private string? _siteId;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public OmadaClient(HttpClient http, IOptions<TaproomOptions> options)
    {
        _http = http;
        _options = options.Value.Omada;
    }

    public async Task<IReadOnlyList<OmadaClientRecord>> GetClientsAsync(CancellationToken cancellationToken)
    {
        var siteId = await GetSiteIdAsync(cancellationToken);

        var clients = await GetAsync<OmadaPagedResult<OmadaClientDto>>(
            $"/openapi/v1/{_omadacId}/sites/{siteId}/clients?page=1&pageSize=500", cancellationToken);
        var devices = await GetAsync<OmadaPagedResult<OmadaDeviceDto>>(
            $"/openapi/v1/{_omadacId}/sites/{siteId}/devices?page=1&pageSize=500", cancellationToken);

        var apNameByMac = new Dictionary<string, string>();
        foreach (var device in devices?.Data ?? [])
        {
            var mac = MacAddress.Normalize(device.Mac);
            if (mac is not null && !string.IsNullOrWhiteSpace(device.Name))
            {
                apNameByMac[mac] = device.Name!;
            }
        }

        var now = DateTimeOffset.UtcNow;
        var result = new List<OmadaClientRecord>();
        foreach (var c in clients?.Data ?? [])
        {
            var mac = MacAddress.Normalize(c.Mac);
            if (mac is null) continue;

            var apMac = MacAddress.Normalize(c.ApMac);
            var lastSeen = c.LastSeen is long ms and > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : now;

            result.Add(new OmadaClientRecord
            {
                Mac = mac,
                CustomName = c.Name,
                Hostname = c.HostName,
                Ip = c.Ip,
                Wireless = c.Wireless,
                Active = c.Active,
                Ssid = c.Wireless ? c.Ssid : null,
                ApName = c.Wireless && apMac is not null && apNameByMac.TryGetValue(apMac, out var apName) ? apName : null,
                Band = c.Wireless ? BandFromRadioId(c.RadioId) : null,
                Rssi = c.Wireless ? c.Rssi : null,
                RxBytes = c.TrafficDown,
                TxBytes = c.TrafficUp,
                ConnectedSince = c.Uptime is long secs and > 0 ? now - TimeSpan.FromSeconds(secs) : null,
                LastSeen = lastSeen,
            });
        }

        return result;
    }

    private static string? BandFromRadioId(int? radioId) => radioId switch
    {
        0 => "2.4 GHz",
        1 => "5 GHz",
        2 => "6 GHz",
        _ => null,
    };

    private async Task<string> GetSiteIdAsync(CancellationToken cancellationToken)
    {
        if (_siteId is not null)
        {
            return _siteId;
        }

        await EnsureOmadacIdAsync(cancellationToken);
        var sites = await GetAsync<OmadaPagedResult<OmadaSiteDto>>(
            $"/openapi/v1/{_omadacId}/sites?page=1&pageSize=100", cancellationToken);

        var site = sites?.Data.FirstOrDefault(s => s.Name == _options.SiteName) ?? sites?.Data.FirstOrDefault();
        if (site is null)
        {
            throw new InvalidOperationException($"Omada site '{_options.SiteName}' not found.");
        }

        _siteId = site.SiteId;
        return _siteId;
    }

    private async Task EnsureOmadacIdAsync(CancellationToken cancellationToken)
    {
        if (_omadacId is not null)
        {
            return;
        }

        var response = await _http.GetFromJsonAsync<OmadaEnvelope<OmadaInfoResult>>("/api/info", cancellationToken);
        _omadacId = response?.Result?.OmadacId
            ?? throw new InvalidOperationException("Omada /api/info did not return an omadacId.");
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Authorization", $"AccessToken={token}");

        var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            token = await GetAccessTokenAsync(cancellationToken, forceRefresh: true);
            using var retry = new HttpRequestMessage(HttpMethod.Get, path);
            retry.Headers.TryAddWithoutValidation("Authorization", $"AccessToken={token}");
            response = await _http.SendAsync(retry, cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<OmadaEnvelope<T>>(cancellationToken);
        if (envelope is null || envelope.ErrorCode != 0)
        {
            throw new InvalidOperationException($"Omada API error at {path}: {envelope?.ErrorCode} {envelope?.Msg}");
        }

        return envelope.Result;
    }

    /// <summary>
    /// Omada's own docs only show grant_type/client_id/client_secret/omadacId in the JSON body, but
    /// the controller actually requires them duplicated as URL query parameters too — a documented
    /// bug in the official docs (confirmed against a live controller, error -44106 "Client Id Or
    /// Client Secret Is Invalid" otherwise, regardless of how valid the credentials actually are).
    /// See https://community.tp-link.com/en/home/forum/topic/655788.
    /// </summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        if (!forceRefresh && _accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _accessToken;
        }

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _accessToken;
            }

            await EnsureOmadacIdAsync(cancellationToken);

            var clientId = Uri.EscapeDataString(_options.ClientId);
            var clientSecret = Uri.EscapeDataString(_options.ClientSecret);
            var omadacId = Uri.EscapeDataString(_omadacId!);
            var path = $"/openapi/authorize/token?grant_type=client_credentials&client_id={clientId}&client_secret={clientSecret}&omadacId={omadacId}";

            var body = new OmadaTokenRequest
            {
                OmadacId = _omadacId!,
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret,
            };

            var response = await _http.PostAsJsonAsync(path, body, cancellationToken);
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content.ReadFromJsonAsync<OmadaEnvelope<OmadaTokenResult>>(cancellationToken);
            if (envelope is null || envelope.ErrorCode != 0 || envelope.Result is null)
            {
                throw new InvalidOperationException($"Omada token request failed: {envelope?.ErrorCode} {envelope?.Msg}");
            }

            _accessToken = envelope.Result.AccessToken;
            // Refresh a little early so a request never races an expiry.
            _tokenExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(Math.Max(30, envelope.Result.ExpiresIn - 60));
            return _accessToken;
        }
        finally
        {
            _authLock.Release();
        }
    }
}
