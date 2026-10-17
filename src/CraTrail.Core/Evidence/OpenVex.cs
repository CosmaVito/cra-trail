using System.Globalization;
using System.Text.Json.Nodes;
using CraTrail.Core.Json;

namespace CraTrail.Core.Evidence;

public sealed record VexStatement(
    IReadOnlyList<string> VulnerabilityIds,
    IReadOnlyList<string> Products,
    bool AllProducts,
    string Status,
    string? Justification,
    string? ImpactStatement,
    DateTimeOffset? Timestamp,
    int Order);

public sealed class VexIndex(IReadOnlyList<VexStatement> statements)
{
    public static VexIndex Empty { get; } = new([]);

    public IReadOnlyList<VexStatement> Statements { get; } = statements;

    public VexStatement? Resolve(IReadOnlyCollection<string> vulnerabilityIds, string? purl) =>
        Statements
            .Where(s => s.VulnerabilityIds.Any(id => vulnerabilityIds.Contains(id, StringComparer.OrdinalIgnoreCase)))
            .Where(s => s.AllProducts || s.Products.Any(p => Purl.Matches(p, purl)))
            .OrderBy(s => s.Timestamp ?? DateTimeOffset.MinValue)
            .ThenBy(s => s.Order)
            .LastOrDefault();
}

public static class OpenVex
{
    public const string Context = "https://openvex.dev/ns/v0.2.0";

    public static IReadOnlyList<string> KnownStatuses { get; } = ["not_affected", "affected", "fixed", "under_investigation"];

    public static IReadOnlyList<VexStatement> ParseStatements(JsonNode document, int firstOrder = 0)
    {
        var context = JsonAccess.Text(JsonAccess.Child(document, "@context"));
        if (context is null || !context.StartsWith("https://openvex.dev/ns", StringComparison.Ordinal))
            throw new FormatException("Not an OpenVEX document: '@context' is missing or unknown.");

        var documentTime = ParseTime(JsonAccess.Child(document, "timestamp"));
        var statements = new List<VexStatement>();
        var order = firstOrder;
        foreach (var item in JsonAccess.Items(JsonAccess.Child(document, "statements")))
        {
            var vulnerability = JsonAccess.Child(item, "vulnerability");
            var name = JsonAccess.Text(JsonAccess.Child(vulnerability, "name"))
                ?? throw new FormatException("OpenVEX statement without vulnerability.name.");
            var ids = new List<string> { name };
            ids.AddRange(JsonAccess.Items(JsonAccess.Child(vulnerability, "aliases")).Select(JsonAccess.Text).OfType<string>());

            var status = JsonAccess.Text(JsonAccess.Child(item, "status"));
            if (status is null || !KnownStatuses.Contains(status))
                throw new FormatException($"OpenVEX statement for {name} has an unknown status '{status}'.");

            var productNodes = JsonAccess.Items(JsonAccess.Child(item, "products")).ToList();
            var products = new List<string>();
            foreach (var product in productNodes)
            {
                // Subcomponents are where the vulnerability originates, usually dependencies, and Grype reports
                // findings on those. A product that lists them speaks about them, whatever its own identifier.
                var subcomponents = JsonAccess.Items(JsonAccess.Child(product, "subcomponents")).ToList();
                foreach (var target in subcomponents.Count > 0 ? subcomponents : [product])
                {
                    string?[] candidates =
                    [
                        JsonAccess.Text(JsonAccess.Child(target, "@id")),
                        JsonAccess.Text(JsonAccess.Child(JsonAccess.Child(target, "identifiers"), "purl")),
                    ];
                    foreach (var candidate in candidates)
                    {
                        if (candidate is not null && candidate.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase) && !products.Contains(candidate))
                            products.Add(candidate);
                    }
                }
            }

            statements.Add(new VexStatement(
                ids,
                products,
                productNodes.Count == 0,
                status,
                JsonAccess.Text(JsonAccess.Child(item, "justification")),
                JsonAccess.Text(JsonAccess.Child(item, "impact_statement")),
                ParseTime(JsonAccess.Child(item, "timestamp")) ?? documentTime,
                order++));
        }
        return statements;
    }

    public static VexIndex BuildIndex(IEnumerable<JsonNode> documents)
    {
        var statements = new List<VexStatement>();
        foreach (var document in documents)
            statements.AddRange(ParseStatements(document, statements.Count));
        return new VexIndex(statements);
    }

    public static JsonObject Merge(IEnumerable<JsonNode> documents, string id, string author, DateTimeOffset timestamp)
    {
        var statements = new JsonArray();
        foreach (var document in documents)
        {
            ParseStatements(document);
            var documentTime = JsonAccess.Text(JsonAccess.Child(document, "timestamp"));
            foreach (var item in JsonAccess.Items(JsonAccess.Child(document, "statements")))
            {
                var copy = (JsonObject)item.DeepClone();
                if (JsonAccess.Child(copy, "timestamp") is null && documentTime is not null)
                    copy["timestamp"] = documentTime;
                statements.Add(copy);
            }
        }
        return new JsonObject
        {
            ["@context"] = Context,
            ["@id"] = id,
            ["author"] = author,
            ["timestamp"] = Timestamps.Format(timestamp),
            ["version"] = 1,
            ["statements"] = statements,
        };
    }

    private static DateTimeOffset? ParseTime(JsonNode? node) =>
        DateTimeOffset.TryParse(JsonAccess.Text(node), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time
            : null;
}
