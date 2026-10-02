using System.Text.Json;
using System.Text.RegularExpressions;

namespace Taproom.Api.Clients;

public enum PtrLookupKind
{
    /// <summary>A PTR name was returned.</summary>
    Found,
    /// <summary>The resolver definitively has no name (NXDOMAIN, or NOERROR with no PTR record).</summary>
    NoName,
    /// <summary>The lookup itself failed (SERVFAIL, REFUSED, timeout, unexpected reply) — worth retrying later.</summary>
    Error,
}

public sealed record PtrLookupOutcome(PtrLookupKind Kind, string? Name = null);

/// <summary>
/// Interprets OPNsense's DNS Lookup reply, { "result": "ok", "response": { "PTR": { "answers": [...] } } }.
/// A found name is a dig-style record line ending "PTR &lt;name&gt;" (tab-separated for IPv4, space-separated
/// for IPv6); a failure is a plain message such as "Host … not found: 3(NXDOMAIN)".
/// </summary>
public static partial class PtrAnswerParser
{
    [GeneratedRegex(@"\sPTR\s+(\S+)\s*$")]
    private static partial Regex PtrLine();

    public static PtrLookupOutcome Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("result", out var result) || result.GetString() != "ok"
            || !root.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object
            || !response.TryGetProperty("PTR", out var ptr) || ptr.ValueKind != JsonValueKind.Object
            || !ptr.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Array)
        {
            return new PtrLookupOutcome(PtrLookupKind.Error);
        }

        var lines = answers.EnumerateArray().Select(a => a.GetString() ?? "").ToList();

        foreach (var line in lines)
        {
            var match = PtrLine().Match(line);
            if (match.Success)
            {
                return new PtrLookupOutcome(PtrLookupKind.Found, match.Groups[1].Value.TrimEnd('.'));
            }
        }

        if (lines.Count == 0
            || lines.Any(l => l.Contains("NXDOMAIN", StringComparison.Ordinal) || l.Contains("has no PTR record", StringComparison.Ordinal)))
        {
            return new PtrLookupOutcome(PtrLookupKind.NoName);
        }

        return new PtrLookupOutcome(PtrLookupKind.Error);
    }
}
