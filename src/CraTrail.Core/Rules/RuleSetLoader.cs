using System.Text.Json.Nodes;
using CraTrail.Core.Json;

namespace CraTrail.Core.Rules;

public static class RuleSetLoader
{
    private static readonly string[] SarifLevels = ["error", "warning", "note", "none"];

    public static RuleSet Load(string crosswalkYaml, IEnumerable<string> profileYamls)
    {
        ArgumentNullException.ThrowIfNull(profileYamls);
        var errors = new List<string>();

        var profiles = new Dictionary<string, FieldProfile>(StringComparer.Ordinal);
        foreach (var yaml in profileYamls)
        {
            var profile = ReadProfile(ParseYaml(yaml, "profile", errors), errors);
            if (profile is not null && !profiles.TryAdd(profile.Id, profile))
                errors.Add($"Duplicate profile '{profile.Id}'.");
        }

        var root = ParseYaml(crosswalkYaml, "crosswalk", errors);
        var version = JsonAccess.Text(JsonAccess.Child(root, "version"));
        if (string.IsNullOrWhiteSpace(version))
            errors.Add("crosswalk: 'version' must be a non-empty string.");

        var checks = ReadChecks(JsonAccess.Child(root, "checks"), profiles, errors);
        var checkIds = checks.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var requirements = ReadRequirements(JsonAccess.Child(root, "requirements"), checkIds, errors);
        var gate = ReadStringList(JsonAccess.Child(root, "gate"), "gate", errors);
        foreach (var id in gate.Where(id => !checkIds.Contains(id)))
            errors.Add($"gate: unknown check '{id}'.");

        if (errors.Count > 0)
            throw new RuleSetException(errors);
        return new RuleSet(version!, gate, checks, requirements, profiles);
    }

    private static JsonNode? ParseYaml(string yaml, string what, List<string> errors)
    {
        try
        {
            return YamlToJson.Convert(yaml);
        }
        catch (FormatException ex)
        {
            errors.Add($"{what}: {ex.Message}");
            return null;
        }
    }

    private static List<CheckDefinition> ReadChecks(JsonNode? node, Dictionary<string, FieldProfile> profiles, List<string> errors)
    {
        var checks = new List<CheckDefinition>();
        if (node is not JsonObject map)
        {
            errors.Add("crosswalk: 'checks' must be a mapping.");
            return checks;
        }
        foreach (var (id, value) in map)
        {
            if (value is not JsonObject body)
            {
                errors.Add($"check '{id}': must be a mapping.");
                continue;
            }
            var parameters = (JsonObject)body.DeepClone();
            parameters.Remove("type");
            parameters.Remove("hint");
            var check = new CheckDefinition(
                id,
                JsonAccess.Text(JsonAccess.Child(body, "type")) ?? "",
                parameters,
                JsonAccess.Text(JsonAccess.Child(body, "hint")));
            ValidateCheck(check, profiles, errors);
            checks.Add(check);
        }
        return checks;
    }

    private static void ValidateCheck(CheckDefinition check, Dictionary<string, FieldProfile> profiles, List<string> errors)
    {
        var where = $"check '{check.Id}'";
        switch (check.Type)
        {
            case CheckTypes.FilePresent:
                RequireKind(check, where, errors);
                break;
            case CheckTypes.JsonFieldRequired:
                RequireKind(check, where, errors);
                var profile = check.GetString("profile");
                if (profile is null || !profiles.ContainsKey(profile))
                    errors.Add($"{where}: unknown profile '{profile}'.");
                break;
            case CheckTypes.ScanThreshold:
                RequireKind(check, where, errors);
                if (check.GetNumber("max-results") is < 0)
                    errors.Add($"{where}: 'max-results' must be zero or more.");
                foreach (var level in check.GetStrings("levels").Where(l => !SarifLevels.Contains(l)))
                    errors.Add($"{where}: unknown SARIF level '{level}'.");
                break;
            case CheckTypes.VexCovers:
                if (check.GetString("kind") != "grype")
                    errors.Add($"{where}: 'kind' must be grype.");
                break;
            case CheckTypes.AttestedField:
                var field = check.GetString("field");
                if (field is null
                    || !(field.StartsWith("product.", StringComparison.Ordinal) || field.StartsWith("manifest.", StringComparison.Ordinal))
                    || !JsonSelect.IsValid(field))
                    errors.Add($"{where}: 'field' must be a path starting with 'product.' or 'manifest.'.");
                break;
            default:
                errors.Add($"{where}: unknown type '{check.Type}'.");
                break;
        }
    }

    private static void RequireKind(CheckDefinition check, string where, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(check.GetString("kind")))
            errors.Add($"{where}: 'kind' must be a non-empty string.");
    }

    private static List<Requirement> ReadRequirements(JsonNode? node, HashSet<string> checkIds, List<string> errors)
    {
        var requirements = new List<Requirement>();
        if (node is not JsonArray items)
        {
            errors.Add("crosswalk: 'requirements' must be a list.");
            return requirements;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var id = JsonAccess.Text(JsonAccess.Child(item, "id")) ?? "";
            var where = $"requirement '{id}'";
            if (!IsValidId(id))
                errors.Add($"{where}: id must use lowercase letters, digits, '-' and '/'.");
            else if (!seen.Add(id))
                errors.Add($"{where}: duplicate id.");

            var point = RequireText(item, "point", where, errors);
            var title = RequireText(item, "title", where, errors);
            var source = RequireText(item, "source", where, errors);
            var checks = ReadStringList(JsonAccess.Child(item, "checks"), $"{where} checks", errors);
            foreach (var check in checks.Where(c => !checkIds.Contains(c)))
                errors.Add($"{where}: unknown check '{check}'.");

            var coverage = JsonAccess.Text(JsonAccess.Child(item, "coverage")) ?? "";
            if (coverage is not (Coverage.Automated or Coverage.Partial or Coverage.Manual))
                errors.Add($"{where}: coverage must be automated, partial or manual.");
            else if (coverage == Coverage.Manual && checks.Count > 0)
                errors.Add($"{where}: manual coverage cannot list checks.");
            else if (coverage != Coverage.Manual && checks.Count == 0)
                errors.Add($"{where}: {coverage} coverage needs at least one check.");

            requirements.Add(new Requirement(id, point, title, source, coverage, checks, ReadRefs(JsonAccess.Child(item, "refs"), where, errors)));
        }
        return requirements;
    }

    private static Dictionary<string, IReadOnlyList<string>> ReadRefs(JsonNode? node, string where, List<string> errors)
    {
        var refs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (node is null)
            return refs;
        if (node is not JsonObject map)
        {
            errors.Add($"{where}: 'refs' must be a mapping.");
            return refs;
        }
        foreach (var (key, value) in map)
            refs[key] = ReadStringList(value, $"{where} refs", errors);
        return refs;
    }

    private static FieldProfile? ReadProfile(JsonNode? root, List<string> errors)
    {
        var id = JsonAccess.Text(JsonAccess.Child(root, "id"));
        if (string.IsNullOrWhiteSpace(id))
        {
            errors.Add("profile: 'id' must be a non-empty string.");
            return null;
        }
        var where = $"profile '{id}'";
        var title = RequireText(root, "title", where, errors);
        var fields = new List<FieldRule>();
        if (JsonAccess.Child(root, "fields") is not JsonArray { Count: > 0 } items)
            errors.Add($"{where}: 'fields' must be a non-empty list.");
        else
            fields.AddRange(items.Select(item => ReadField(item, where, errors)));
        return new FieldProfile(id, title, JsonAccess.Text(JsonAccess.Child(root, "source")), fields);
    }

    private static FieldRule ReadField(JsonNode? item, string profileWhere, List<string> errors)
    {
        var id = JsonAccess.Text(JsonAccess.Child(item, "id")) ?? "";
        var where = $"{profileWhere} field '{id}'";
        if (id.Length == 0)
            errors.Add($"{where}: 'id' is required.");

        var each = JsonAccess.Text(JsonAccess.Child(item, "each"));
        if (each is not (null or EachScopes.Components or EachScopes.AllComponents))
            errors.Add($"{where}: 'each' must be components or all-components.");

        var paths = new List<string>();
        if (JsonAccess.Text(JsonAccess.Child(item, "path")) is { } single)
            paths.Add(single);
        paths.AddRange(ReadStringList(JsonAccess.Child(item, "paths"), where, errors));
        if (paths.Count == 0)
            errors.Add($"{where}: 'path' or 'paths' is required.");
        foreach (var path in paths.Where(p => !JsonSelect.IsValid(p)))
            errors.Add($"{where}: invalid path '{path}'.");

        var require = ReadRequire(JsonAccess.Child(item, "require"), where, errors);
        var skipAssemblies = JsonAccess.Bool(JsonAccess.Child(item, "skip-assemblies")) ?? false;
        return new FieldRule(id, each, paths, require, skipAssemblies, JsonAccess.Text(JsonAccess.Child(item, "ref")));
    }

    private static FieldRequirement ReadRequire(JsonNode? node, string where, List<string> errors)
    {
        if (JsonAccess.Text(node) is FieldRequirementKinds.Present or FieldRequirementKinds.NonEmpty or FieldRequirementKinds.Absent)
            return new FieldRequirement(JsonAccess.Text(node)!, []);
        if (JsonAccess.Text(JsonAccess.Child(node, FieldRequirementKinds.Contains)) is { } value)
            return new FieldRequirement(FieldRequirementKinds.Contains, [value]);
        if (JsonAccess.Child(node, FieldRequirementKinds.OneOf) is JsonArray)
        {
            var values = ReadStringList(JsonAccess.Child(node, FieldRequirementKinds.OneOf), where, errors);
            if (values.Count > 0)
                return new FieldRequirement(FieldRequirementKinds.OneOf, values);
        }
        errors.Add($"{where}: 'require' must be present, non-empty, absent, {{ contains: X }} or {{ one-of: [...] }}.");
        return new FieldRequirement(FieldRequirementKinds.Present, []);
    }

    private static List<string> ReadStringList(JsonNode? node, string where, List<string> errors)
    {
        if (node is null)
            return [];
        if (node is not JsonArray items || items.Any(i => JsonAccess.Text(i) is null))
        {
            errors.Add($"{where}: must be a list of strings.");
            return [];
        }
        return [.. items.Select(i => JsonAccess.Text(i)!)];
    }

    private static string RequireText(JsonNode? node, string key, string where, List<string> errors)
    {
        var text = JsonAccess.Text(JsonAccess.Child(node, key));
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add($"{where}: '{key}' must be a non-empty string.");
            return "";
        }
        return text;
    }

    private static bool IsValidId(string id) =>
        id.Length > 0 && id.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '/');
}
