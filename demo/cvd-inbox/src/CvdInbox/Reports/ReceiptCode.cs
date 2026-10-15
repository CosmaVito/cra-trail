using System.Security.Cryptography;
using System.Text;

namespace CvdInbox.Reports;

// 128 random bits shown as eight groups of four hex digits. Only the SHA-256 of the code is stored,
// so a copy of the database does not let anyone follow a report.
public static class ReceiptCode
{
    public static string Create()
    {
        var hex = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        return string.Join('-', Enumerable.Range(0, 8).Select(group => hex.Substring(group * 4, 4)));
    }

    public static string? Normalize(string? typed)
    {
        if (typed is null)
            return null;
        var compact = string.Concat(typed.Where(c => c != '-' && !char.IsWhiteSpace(c))).ToLowerInvariant();
        return compact.Length == 32 && compact.All(char.IsAsciiHexDigitLower) ? compact : null;
    }

    public static string Hash(string normalized) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(normalized)));
}
