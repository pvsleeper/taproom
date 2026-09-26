using System.Text;

namespace Taproom.Api.Clients;

public static class MacAddress
{
    /// <summary>Normalizes any separator style (colon, dash, none) to upper-case colon form, e.g. "AA:BB:CC:DD:EE:FF".</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var hex = new StringBuilder(12);
        foreach (var c in raw)
        {
            if (Uri.IsHexDigit(c))
            {
                hex.Append(char.ToUpperInvariant(c));
            }
        }

        if (hex.Length != 12)
        {
            return null;
        }

        var sb = new StringBuilder(17);
        for (var i = 0; i < 12; i += 2)
        {
            if (i > 0)
            {
                sb.Append(':');
            }
            sb.Append(hex[i]);
            sb.Append(hex[i + 1]);
        }

        return sb.ToString();
    }
}
