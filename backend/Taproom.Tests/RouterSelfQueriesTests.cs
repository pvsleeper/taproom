using Taproom.Api.Clients;

namespace Taproom.Tests;

public class RouterSelfQueriesTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("LocalHost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void Router_originated_clients_are_recognised(string client)
    {
        Assert.True(RouterSelfQueries.IsRouterSelf(client));
    }

    [Theory]
    [InlineData("10.0.1.238")]
    [InlineData("serifonium.dev")]
    [InlineData("")]
    [InlineData(null)]
    public void Real_devices_are_not(string? client)
    {
        Assert.False(RouterSelfQueries.IsRouterSelf(client));
    }
}
