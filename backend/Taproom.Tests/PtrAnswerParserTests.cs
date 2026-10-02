using Taproom.Api.Clients;

namespace Taproom.Tests;

public class PtrAnswerParserTests
{
    private static string Reply(params string[] answers) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            result = "ok",
            response = new { PTR = new { answers, query_time = "0 ms", server = "127.0.0.1" } },
        });

    [Fact]
    public void Ipv4_answer_line_yields_the_name_without_the_trailing_dot()
    {
        var outcome = PtrAnswerParser.Parse(Reply("8.8.8.8.in-addr.arpa.\t31316\tIN\tPTR\tdns.google."));

        Assert.Equal(new PtrLookupOutcome(PtrLookupKind.Found, "dns.google"), outcome);
    }

    [Fact]
    public void Ipv6_answer_line_is_space_separated()
    {
        var outcome = PtrAnswerParser.Parse(Reply(
            "1.1.1.1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.7.4.0.0.7.4.6.0.6.2.ip6.arpa. 1800 IN PTR one.one.one.one."));

        Assert.Equal("one.one.one.one", outcome.Name);
    }

    [Fact]
    public void A_cname_hop_before_the_ptr_line_is_skipped()
    {
        var outcome = PtrAnswerParser.Parse(Reply(
            "5.2.0.192.in-addr.arpa.\t60\tIN\tCNAME\t5.0-26.2.0.192.in-addr.arpa.",
            "5.0-26.2.0.192.in-addr.arpa.\t60\tIN\tPTR\thost.example.net."));

        Assert.Equal("host.example.net", outcome.Name);
    }

    [Fact]
    public void Nxdomain_message_is_a_definitive_no_name()
    {
        var outcome = PtrAnswerParser.Parse(Reply("Host 4.3.2.1.in-addr.arpa not found: 3(NXDOMAIN)"));

        Assert.Equal(PtrLookupKind.NoName, outcome.Kind);
    }

    [Fact]
    public void An_empty_answer_list_is_no_name()
    {
        Assert.Equal(PtrLookupKind.NoName, PtrAnswerParser.Parse(Reply()).Kind);
    }

    [Theory]
    [InlineData("Host 4.3.2.1.in-addr.arpa not found: 2(SERVFAIL)")]
    [InlineData("Host 4.3.2.1.in-addr.arpa not found: 5(REFUSED)")]
    [InlineData(";; connection timed out; no servers could be reached")]
    public void Resolver_failures_are_errors_not_no_name(string message)
    {
        Assert.Equal(PtrLookupKind.Error, PtrAnswerParser.Parse(Reply(message)).Kind);
    }

    [Theory]
    [InlineData("{\"result\":\"failed\"}")]
    [InlineData("{\"status\":403,\"message\":\"Forbidden\"}")]
    [InlineData("{\"result\":\"ok\",\"response\":{}}")]
    public void Unexpected_replies_are_errors(string json)
    {
        Assert.Equal(PtrLookupKind.Error, PtrAnswerParser.Parse(json).Kind);
    }
}
