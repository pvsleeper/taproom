using Taproom.Api.Sources.Opnsense;

namespace Taproom.Tests;

public class ClientMatchingTests
{
    [Fact]
    public void Matches_when_client_field_is_a_resolved_hostname_in_the_match_set()
    {
        var matchNames = new HashSet<string> { "10.0.1.203", "serifonium.dev" };
        Assert.True(ClientMatching.Matches("serifonium.dev", matchNames));
    }

    [Fact]
    public void Matches_when_client_field_is_the_plain_ip_in_the_match_set()
    {
        var matchNames = new HashSet<string> { "10.0.1.203", "serifonium.dev" };
        Assert.True(ClientMatching.Matches("10.0.1.203", matchNames));
    }

    [Fact]
    public void Matching_is_case_insensitive_and_ignores_a_trailing_dot()
    {
        var matchNames = new HashSet<string> { "serifonium.dev" };
        Assert.True(ClientMatching.Matches("Serifonium.Dev.", matchNames));
    }

    [Fact]
    public void Does_not_match_a_different_devices_hostname()
    {
        var matchNames = new HashSet<string> { "10.0.1.203", "serifonium.dev" };
        Assert.False(ClientMatching.Matches("some-other-device.lan", matchNames));
    }

    [Fact]
    public void Does_not_match_when_client_field_is_null()
    {
        var matchNames = new HashSet<string> { "10.0.1.203" };
        Assert.False(ClientMatching.Matches(null, matchNames));
    }

    [Fact]
    public void Substring_overlap_with_another_devices_lookup_of_our_hostname_does_not_match()
    {
        // e.g. searchPhrase="serifonium.dev" also returns rows where some OTHER client looked up
        // "serifonium.dev" as a domain — those rows have a different client field and must be excluded.
        var matchNames = new HashSet<string> { "10.0.1.203", "serifonium.dev" };
        Assert.False(ClientMatching.Matches("10.0.1.55", matchNames));
    }
}
