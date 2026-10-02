namespace Taproom.Api.Clients;

/// <summary>Queries the router makes for itself (including Taproom's PTR lookups, which it runs) are logged with client "localhost" and aren't any device's DNS activity.</summary>
public static class RouterSelfQueries
{
    public static bool IsRouterSelf(string? client) =>
        client is not null
        && (client.Equals("localhost", StringComparison.OrdinalIgnoreCase) || client is "127.0.0.1" or "::1");
}
