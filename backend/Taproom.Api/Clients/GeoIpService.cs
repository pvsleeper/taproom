using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Options;
using Taproom.Api.Config;

namespace Taproom.Api.Clients;

/// <summary>
/// Looks up city and ASN info for an IP from local MaxMind GeoLite2 .mmdb files — no per-lookup network
/// calls. If a database file is missing or fails to load, lookups through it simply return null fields
/// rather than throwing, so a missing GeoIP setup degrades the page instead of breaking it.
/// </summary>
public sealed class GeoIpService : IDisposable
{
    private readonly DatabaseReader? _city;
    private readonly DatabaseReader? _asn;
    private readonly ILogger<GeoIpService> _logger;

    public bool Available => _city is not null || _asn is not null;

    public GeoIpService(IOptions<TaproomOptions> options, ILogger<GeoIpService> logger)
    {
        _logger = logger;
        var geoOptions = options.Value.GeoIp;

        _city = TryOpen(geoOptions.CityDbPath, "City");
        _asn = TryOpen(geoOptions.AsnDbPath, "ASN");
    }

    private DatabaseReader? TryOpen(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return new DatabaseReader(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open GeoIP {Label} database at {Path}", label, path);
            return null;
        }
    }

    public GeoInfo? Lookup(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address))
        {
            return null;
        }

        string? country = null, city = null, org = null;
        double? lat = null, lon = null;
        long? asn = null;

        if (_city is not null)
        {
            try
            {
                var response = _city.City(address);
                country = response.Country.IsoCode;
                city = response.City.Name;
                lat = response.Location.Latitude;
                lon = response.Location.Longitude;
            }
            catch (AddressNotFoundException)
            {
                // Not in the database — e.g. a private IP that slipped through. Leave fields null.
            }
        }

        if (_asn is not null)
        {
            try
            {
                var response = _asn.Asn(address);
                asn = response.AutonomousSystemNumber;
                org = response.AutonomousSystemOrganization;
            }
            catch (AddressNotFoundException)
            {
            }
        }

        if (country is null && city is null && org is null && asn is null)
        {
            return null;
        }

        return new GeoInfo { Country = country, City = city, Lat = lat, Lon = lon, Asn = asn, Org = org };
    }

    public void Dispose()
    {
        _city?.Dispose();
        _asn?.Dispose();
    }
}
