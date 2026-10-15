using System.Text.Json.Nodes;
using CraTrail.Core.Json;

namespace CraTrail.Core.Evidence;

public sealed record Finding(
    string Id,
    IReadOnlyList<string> Ids,
    string Name,
    string Version,
    string? Purl,
    string? BomRef,
    string Severity,
    double? MaxCvss,
    double EffectiveScore,
    bool Kev,
    double? Epss,
    IReadOnlyList<string> FixVersions);

public sealed record GrypeReport(
    IReadOnlyList<Finding> Matches,
    IReadOnlyList<Finding> Ignored,
    string? GrypeVersion,
    string? DatabaseBuilt);

public static class Grype
{
    public static GrypeReport Parse(JsonNode report)
    {
        var descriptor = JsonAccess.Child(report, "descriptor");
        if (JsonAccess.Text(JsonAccess.Child(descriptor, "name")) != "grype")
            throw new FormatException("Not a Grype JSON report: descriptor.name is not 'grype'.");

        return new GrypeReport(
            [.. JsonAccess.Items(JsonAccess.Child(report, "matches")).Select(ReadFinding)],
            [.. JsonAccess.Items(JsonAccess.Child(report, "ignoredMatches")).Select(ReadFinding)],
            JsonAccess.Text(JsonAccess.Child(descriptor, "version")),
            JsonAccess.Text(JsonAccess.Child(JsonAccess.Child(JsonAccess.Child(descriptor, "db"), "status"), "built")));
    }

    public static double SeverityScore(string severity) => severity.ToUpperInvariant() switch
    {
        "CRITICAL" => 9.0,
        "HIGH" => 7.0,
        "MEDIUM" => 4.0,
        "LOW" => 0.1,
        _ => 0.0,
    };

    private static Finding ReadFinding(JsonNode match)
    {
        var vulnerability = JsonAccess.Child(match, "vulnerability");
        var id = JsonAccess.Text(JsonAccess.Child(vulnerability, "id"))
            ?? throw new FormatException("Grype match without vulnerability.id.");
        var related = JsonAccess.Items(JsonAccess.Child(match, "relatedVulnerabilities")).ToList();
        List<JsonNode?> records = [vulnerability, .. related];

        var ids = new List<string> { id };
        foreach (var relatedId in related.Select(r => JsonAccess.Text(JsonAccess.Child(r, "id"))).OfType<string>())
        {
            if (!ids.Contains(relatedId, StringComparer.OrdinalIgnoreCase))
                ids.Add(relatedId);
        }

        var scores = records
            .SelectMany(r => JsonAccess.Items(JsonAccess.Child(r, "cvss")))
            .Select(c => JsonAccess.Number(JsonAccess.Child(JsonAccess.Child(c, "metrics"), "baseScore")))
            .OfType<double>()
            .ToList();
        double? maxCvss = scores.Count > 0 ? scores.Max() : null;
        var epss = records
            .SelectMany(r => JsonAccess.Items(JsonAccess.Child(r, "epss")))
            .Select(e => JsonAccess.Number(JsonAccess.Child(e, "epss")))
            .OfType<double>()
            .ToList();
        var severity = JsonAccess.Text(JsonAccess.Child(vulnerability, "severity")) ?? "Unknown";
        var artifact = JsonAccess.Child(match, "artifact");
        var purl = JsonAccess.Text(JsonAccess.Child(artifact, "purl"));

        return new Finding(
            id,
            ids,
            JsonAccess.Text(JsonAccess.Child(artifact, "name")) ?? "",
            JsonAccess.Text(JsonAccess.Child(artifact, "version")) ?? "",
            string.IsNullOrWhiteSpace(purl) ? null : purl,
            JsonAccess.Text(JsonAccess.Child(artifact, "id")),
            severity,
            maxCvss,
            maxCvss ?? SeverityScore(severity),
            records.Any(r => JsonAccess.Child(r, "knownExploited") is JsonArray { Count: > 0 }),
            epss.Count > 0 ? epss.Max() : null,
            [.. JsonAccess.Items(JsonAccess.Child(JsonAccess.Child(vulnerability, "fix"), "versions")).Select(JsonAccess.Text).OfType<string>()]);
    }
}
