using Taproom.Api.Clients;

namespace Taproom.Tests;

public class PublicAddressTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("142.250.195.225")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.0")]
    [InlineData("172.15.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("223.255.255.255")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2a00:1450:4001:81a::200e")]
    public void Public_unicast_addresses_are_eligible(string ip)
    {
        Assert.True(PrivateIp.IsPubliclyRoutableUnicast(ip));
    }

    [Theory]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("239.255.255.250")]
    [InlineData("240.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("0.1.2.3")]
    [InlineData("10.0.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.10.10")]
    [InlineData("127.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("198.18.0.1")]
    [InlineData("203.0.113.10")]
    [InlineData("ff02::fb")]
    [InlineData("ff00::1")]
    [InlineData("fe80::1")]
    [InlineData("fe80::2d0:b4ff:fe06:84fa%igc0")]
    [InlineData("fd12:3456:789a::1")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:255.255.255.255")]
    [InlineData("not-an-ip")]
    [InlineData("")]
    public void Everything_else_is_not_eligible(string ip)
    {
        Assert.False(PrivateIp.IsPubliclyRoutableUnicast(ip));
    }

    [Fact]
    public void Ipv4_mapped_ipv6_is_judged_as_its_ipv4_address()
    {
        Assert.True(PrivateIp.IsPubliclyRoutableUnicast("::ffff:8.8.8.8"));
    }
}
