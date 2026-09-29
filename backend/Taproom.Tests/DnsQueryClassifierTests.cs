using Taproom.Api.Sources.Opnsense;

namespace Taproom.Tests;

public class DnsQueryClassifierTests
{
    [Fact]
    public void Pass_with_noerror_is_neither_blocked_nor_failed()
    {
        Assert.False(DnsQueryClassifier.IsBlocked("Pass", null));
        Assert.False(DnsQueryClassifier.IsFailed("Pass", null, "NOERROR"));
    }

    [Theory]
    [InlineData("Block")]
    [InlineData("Drop")]
    public void Non_pass_action_is_blocked_not_failed(string action)
    {
        Assert.True(DnsQueryClassifier.IsBlocked(action, null));
        Assert.False(DnsQueryClassifier.IsFailed(action, null, "NOERROR"));
    }

    [Fact]
    public void Pass_with_a_matched_blocklist_is_blocked_not_failed()
    {
        Assert.True(DnsQueryClassifier.IsBlocked("Pass", "hagezi-pro"));
        Assert.False(DnsQueryClassifier.IsFailed("Pass", "hagezi-pro", "NOERROR"));
    }

    [Theory]
    [InlineData("NXDOMAIN")]
    [InlineData("SERVFAIL")]
    [InlineData("REFUSED")]
    public void Pass_with_a_non_noerror_rcode_is_failed_not_blocked(string rcode)
    {
        Assert.False(DnsQueryClassifier.IsBlocked("Pass", null));
        Assert.True(DnsQueryClassifier.IsFailed("Pass", null, rcode));
    }

    [Fact]
    public void Rcode_is_case_insensitive_for_noerror()
    {
        Assert.False(DnsQueryClassifier.IsFailed("Pass", null, "noerror"));
    }
}
