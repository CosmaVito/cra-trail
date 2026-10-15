using System.Text.Json.Nodes;
using CraTrail.Core.Json;

namespace CraTrail.Core.Rules;

public sealed record RuleSet(
    string Version,
    IReadOnlyList<string> Gate,
    IReadOnlyList<CheckDefinition> Checks,
    IReadOnlyList<Requirement> Requirements,
    IReadOnlyDictionary<string, FieldProfile> Profiles)
{
    public CheckDefinition? FindCheck(string id) => Checks.FirstOrDefault(c => c.Id == id);

    public Requirement? FindRequirement(string id) => Requirements.FirstOrDefault(r => r.Id == id);
}

public sealed record Requirement(
    string Id,
    string Point,
    string Title,
    string Source,
    string Coverage,
    IReadOnlyList<string> Checks,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Refs);

public sealed record CheckDefinition(string Id, string Type, JsonObject Parameters, string? Hint)
{
    public string? GetString(string name) => JsonAccess.Text(JsonAccess.Child(Parameters, name));

    public double? GetNumber(string name) => JsonAccess.Number(JsonAccess.Child(Parameters, name));

    public bool? GetBool(string name) => JsonAccess.Bool(JsonAccess.Child(Parameters, name));

    public JsonObject? GetObject(string name) => JsonAccess.Child(Parameters, name) as JsonObject;

    public IReadOnlyList<string> GetStrings(string name) =>
        [.. JsonAccess.Items(JsonAccess.Child(Parameters, name)).Select(JsonAccess.Text).OfType<string>()];

    public CheckDefinition WithOverrides(JsonObject? overrides)
    {
        if (overrides is null || overrides.Count == 0)
            return this;
        var merged = (JsonObject)Parameters.DeepClone();
        foreach (var (key, value) in overrides)
            merged[key] = value?.DeepClone();
        return this with { Parameters = merged };
    }
}

public sealed record FieldProfile(string Id, string Title, string? Source, IReadOnlyList<FieldRule> Fields);

public sealed record FieldRule(
    string Id,
    string? Each,
    IReadOnlyList<string> Paths,
    FieldRequirement Require,
    bool SkipAssemblies,
    string? Reference);

public sealed record FieldRequirement(string Kind, IReadOnlyList<string> Values);

public static class Coverage
{
    public const string Automated = "automated";
    public const string Partial = "partial";
    public const string Manual = "manual";
}

public static class CheckTypes
{
    public const string FilePresent = "file-present";
    public const string JsonFieldRequired = "json-field-required";
    public const string ScanThreshold = "scan-threshold";
    public const string VexCovers = "vex-covers";
    public const string AttestedField = "attested-field";
}

public static class FieldRequirementKinds
{
    public const string Present = "present";
    public const string NonEmpty = "non-empty";
    public const string Absent = "absent";
    public const string Contains = "contains";
    public const string OneOf = "one-of";
}

public static class EachScopes
{
    public const string Components = "components";
    public const string AllComponents = "all-components";
}

public sealed class RuleSetException(IReadOnlyList<string> errors)
    : Exception("Invalid rules:\n" + string.Join("\n", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
