using System.Globalization;
using System.Text.Json.Nodes;
using CraTrail.Core.Json;

namespace CraTrail.Core.Evidence;

public sealed record SarifResult(string Tool, string RuleId, string Level, double? SecuritySeverity, string? Uri, int? StartLine);

public static class Sarif
{
    private static readonly string[] ContentProperties = ["snippet", "contents", "insertedContent"];

    public static IReadOnlyList<SarifResult> ParseResults(JsonNode log)
    {
        if (JsonAccess.Child(log, "runs") is not JsonArray runs)
            throw new FormatException("Not a SARIF log: 'runs' is missing.");

        var results = new List<SarifResult>();
        foreach (var run in runs.OfType<JsonNode>())
        {
            var tool = JsonAccess.Child(run, "tool");
            var driver = JsonAccess.Child(tool, "driver");
            var toolName = JsonAccess.Text(JsonAccess.Child(driver, "name")) ?? "unknown";
            var driverRules = JsonAccess.Child(driver, "rules") as JsonArray;
            var extensions = JsonAccess.Child(tool, "extensions") as JsonArray;

            foreach (var result in JsonAccess.Items(JsonAccess.Child(run, "results")))
            {
                var ruleRef = JsonAccess.Child(result, "rule");
                var ruleId = JsonAccess.Text(JsonAccess.Child(result, "ruleId"))
                    ?? JsonAccess.Text(JsonAccess.Child(ruleRef, "id"))
                    ?? "unknown";
                var rule = FindRule(result, ruleRef, ruleId, driverRules, extensions);
                var level = JsonAccess.Text(JsonAccess.Child(result, "level"))
                    ?? JsonAccess.Text(JsonAccess.Child(JsonAccess.Child(rule, "defaultConfiguration"), "level"))
                    ?? "warning";
                var location = JsonAccess.Child(JsonAccess.Items(JsonAccess.Child(result, "locations")).FirstOrDefault(), "physicalLocation");
                var line = JsonAccess.Number(JsonAccess.Child(JsonAccess.Child(location, "region"), "startLine"));

                results.Add(new SarifResult(
                    toolName,
                    ruleId,
                    level,
                    SecuritySeverity(rule),
                    JsonAccess.Text(JsonAccess.Child(JsonAccess.Child(location, "artifactLocation"), "uri")),
                    line is double value ? (int)value : null));
            }
        }
        return results;
    }

    public static JsonNode Redact(JsonNode log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var copy = log.DeepClone();
        RedactNode(copy);
        return copy;
    }

    // Snippets and file contents can also sit in code flows, fixes and run.artifacts, so the whole log is walked.
    private static void RedactNode(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
                RedactNode(item);
            return;
        }
        if (node is not JsonObject obj)
            return;

        obj.Remove("partialFingerprints");
        foreach (var name in ContentProperties)
        {
            if (obj[name] is JsonObject)
                obj[name] = new JsonObject { ["text"] = "REDACTED" };
        }
        foreach (var (_, child) in obj.ToList())
            RedactNode(child);
    }

    private static JsonNode? FindRule(JsonNode result, JsonNode? ruleRef, string ruleId, JsonArray? driverRules, JsonArray? extensions)
    {
        var index = JsonAccess.Number(JsonAccess.Child(result, "ruleIndex")) ?? JsonAccess.Number(JsonAccess.Child(ruleRef, "index"));
        var component = JsonAccess.Number(JsonAccess.Child(JsonAccess.Child(ruleRef, "toolComponent"), "index"));
        if (component is double c && index is double i)
            return At(JsonAccess.Child(At(extensions, c), "rules") as JsonArray, i);
        if (index is double driverIndex && At(driverRules, driverIndex) is { } byIndex)
            return byIndex;
        return ById(driverRules, ruleId)
            ?? JsonAccess.Items(extensions).Select(e => ById(JsonAccess.Child(e, "rules") as JsonArray, ruleId)).FirstOrDefault(r => r is not null);
    }

    private static JsonNode? At(JsonArray? array, double index) =>
        array is not null && index >= 0 && index < array.Count ? array[(int)index] : null;

    private static JsonNode? ById(JsonArray? rules, string id) =>
        JsonAccess.Items(rules).FirstOrDefault(r => JsonAccess.Text(JsonAccess.Child(r, "id")) == id);

    private static double? SecuritySeverity(JsonNode? rule)
    {
        var value = JsonAccess.Child(JsonAccess.Child(rule, "properties"), "security-severity");
        if (JsonAccess.Number(value) is double number)
            return number;
        return double.TryParse(JsonAccess.Text(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
