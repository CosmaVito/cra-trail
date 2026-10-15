namespace CraTrail.Core.Evidence;

public readonly record struct PurlParts(string Type, string? Namespace, string Name, string? Version);

public static class Purl
{
    public static bool TryParse(string? value, out PurlParts parts)
    {
        parts = default;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = value[4..];
        var cut = rest.IndexOfAny(['?', '#']);
        if (cut >= 0)
            rest = rest[..cut];
        rest = rest.Trim('/');

        string? version = null;
        var at = rest.IndexOf('@', rest.LastIndexOf('/') + 1);
        if (at >= 0)
        {
            version = Uri.UnescapeDataString(rest[(at + 1)..]);
            rest = rest[..at];
        }

        var segments = rest.Split('/');
        if (segments.Length < 2 || segments.Any(s => s.Length == 0))
            return false;
        var ns = segments.Length > 2 ? string.Join('/', segments[1..^1].Select(Uri.UnescapeDataString)) : null;
        parts = new PurlParts(segments[0], ns, Uri.UnescapeDataString(segments[^1]), string.IsNullOrEmpty(version) ? null : version);
        return true;
    }

    public static bool Matches(string? statementPurl, string? findingPurl)
    {
        if (!TryParse(statementPurl, out var statement) || !TryParse(findingPurl, out var finding))
            return false;
        return string.Equals(statement.Type, finding.Type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(statement.Namespace ?? "", finding.Namespace ?? "", StringComparison.OrdinalIgnoreCase)
            && string.Equals(statement.Name, finding.Name, StringComparison.OrdinalIgnoreCase)
            && (statement.Version is null || string.Equals(statement.Version, finding.Version, StringComparison.Ordinal));
    }
}
