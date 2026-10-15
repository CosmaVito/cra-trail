using System.Globalization;
using System.Text.Json.Nodes;
using CraTrail.Core.Json;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Product;

public static class CraConfigLoader
{
    public const string BlockKevCheck = "no-kev-without-vex";
    public const string MaxCvssCheck = "no-critical-without-vex";
    public const string BsiCheck = "sbom-bsi-fields";

    private static readonly string[] RootKeys = ["product", "user-info", "attested", "not-applicable", "gate"];
    private static readonly string[] ProductKeys = ["name", "purl", "support-end", "cvd-contact", "security-txt"];
    private static readonly string[] GateKeys = ["max-cvss-without-vex", "block-kev-without-vex", "require-bsi"];

    public static CraConfig Load(string yaml, string productVersion, RuleSet rules, Func<string, bool> documentExists)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(documentExists);
        var errors = new List<string>();

        JsonNode? root = null;
        try
        {
            root = YamlToJson.Convert(yaml);
        }
        catch (FormatException ex)
        {
            errors.Add(ex.Message);
        }
        if (root is not JsonObject config)
        {
            if (errors.Count == 0)
                errors.Add("cra.yml must be a mapping.");
            throw new ConfigException(errors);
        }

        CheckKeys(config, RootKeys, "cra.yml", errors);
        var product = JsonAccess.Child(config, "product") as JsonObject;
        if (product is null)
        {
            errors.Add("'product' is required.");
            product = new JsonObject();
        }
        CheckKeys(product, ProductKeys, "product", errors);

        var name = OptionalText(product, "name", "product.name", errors);
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("product.name is required.");
        if (string.IsNullOrWhiteSpace(productVersion))
            errors.Add("The product version is required.");

        var purl = OptionalText(product, "purl", "product.purl", errors);
        var cvdContact = OptionalText(product, "cvd-contact", "product.cvd-contact", errors);
        var supportEnd = OptionalText(product, "support-end", "product.support-end", errors);
        if (supportEnd is not null
            && !DateOnly.TryParseExact(supportEnd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            errors.Add("product.support-end must be a date in the form YYYY-MM-DD.");
        var securityTxt = OptionalText(product, "security-txt", "product.security-txt", errors);
        if (securityTxt is not null && !securityTxt.StartsWith("https://", StringComparison.Ordinal))
            errors.Add("product.security-txt must be an https URL.");

        var userInfo = ReadTextMap(JsonAccess.Child(config, "user-info"), "user-info", errors);
        var attestedRaw = ReadTextMap(JsonAccess.Child(config, "attested"), "attested", errors);
        var notApplicable = ReadTextMap(JsonAccess.Child(config, "not-applicable"), "not-applicable", errors);

        var attested = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var sources = new List<string>();
        foreach (var (id, rawPath) in attestedRaw)
        {
            if (rules.FindRequirement(id) is null)
            {
                errors.Add($"attested: unknown requirement '{id}'.");
                continue;
            }
            var path = NormalizeDocumentPath(rawPath);
            if (path is null)
            {
                errors.Add($"attested: '{rawPath}' must be a relative path inside the repository.");
                continue;
            }
            if (!documentExists(path))
            {
                errors.Add($"attested: document not found: {path}");
                continue;
            }
            attested[id] = "attested/" + path;
            if (!sources.Contains(path))
                sources.Add(path);
        }

        foreach (var (id, reason) in notApplicable)
        {
            if (rules.FindRequirement(id) is null)
                errors.Add($"not-applicable: unknown requirement '{id}'.");
            if (string.IsNullOrWhiteSpace(reason))
                errors.Add($"not-applicable: the reason for '{id}' is empty.");
            if (attestedRaw.ContainsKey(id))
                errors.Add($"'{id}' cannot be both attested and not-applicable.");
        }

        var overrides = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        var include = new List<string>();
        var exclude = new List<string>();
        if (JsonAccess.Child(config, "gate") is { } gateNode)
        {
            if (gateNode is not JsonObject gate)
            {
                errors.Add("'gate' must be a mapping.");
            }
            else
            {
                CheckKeys(gate, GateKeys, "gate", errors);
                if (JsonAccess.Child(gate, "max-cvss-without-vex") is { } maxNode)
                {
                    if (JsonAccess.Number(maxNode) is double max and >= 0 and <= 10)
                    {
                        RequireCheck(rules, MaxCvssCheck, "gate.max-cvss-without-vex", errors);
                        overrides[MaxCvssCheck] = new JsonObject { ["select"] = new JsonObject { ["min-cvss"] = max } };
                    }
                    else
                    {
                        errors.Add("gate.max-cvss-without-vex must be a number from 0 to 10.");
                    }
                }
                if (ReadBool(gate, "block-kev-without-vex", errors) is false)
                {
                    RequireCheck(rules, BlockKevCheck, "gate.block-kev-without-vex", errors);
                    exclude.Add(BlockKevCheck);
                }
                if (ReadBool(gate, "require-bsi", errors) is true)
                {
                    RequireCheck(rules, BsiCheck, "gate.require-bsi", errors);
                    include.Add(BsiCheck);
                }
            }
        }

        if (errors.Count > 0)
            throw new ConfigException(errors);

        var info = new ProductInfo(
            name!, productVersion, purl, supportEnd, cvdContact, securityTxt,
            userInfo, attested, new SortedDictionary<string, string>(notApplicable, StringComparer.Ordinal),
            overrides, include, exclude);
        return new CraConfig(info, sources);
    }

    private static void CheckKeys(JsonObject node, string[] allowed, string where, List<string> errors)
    {
        foreach (var key in node.Select(p => p.Key).Where(k => !allowed.Contains(k)))
            errors.Add($"{where}: unknown key '{key}'.");
    }

    private static string? OptionalText(JsonObject node, string key, string where, List<string> errors)
    {
        var value = JsonAccess.Child(node, key);
        if (value is null)
            return null;
        if (JsonAccess.Text(value) is not { } text)
        {
            errors.Add($"{where} must be a string.");
            return null;
        }
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static SortedDictionary<string, string> ReadTextMap(JsonNode? node, string where, List<string> errors)
    {
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (node is null)
            return map;
        if (node is not JsonObject obj)
        {
            errors.Add($"'{where}' must be a mapping.");
            return map;
        }
        foreach (var (key, value) in obj)
        {
            if (value is not null && JsonAccess.Text(value) is null)
                errors.Add($"{where}: the value of '{key}' must be a string.");
            else
                map[key] = JsonAccess.Text(value) ?? "";
        }
        return map;
    }

    private static bool? ReadBool(JsonObject gate, string key, List<string> errors)
    {
        var node = JsonAccess.Child(gate, key);
        if (node is null)
            return null;
        if (JsonAccess.Bool(node) is { } flag)
            return flag;
        errors.Add($"gate.{key} must be true or false.");
        return null;
    }

    private static void RequireCheck(RuleSet rules, string checkId, string option, List<string> errors)
    {
        if (rules.FindCheck(checkId) is null)
            errors.Add($"{option} needs the check '{checkId}' in the rules.");
    }

    private static string? NormalizeDocumentPath(string raw)
    {
        var path = raw.Trim().Replace('\\', '/');
        while (path.StartsWith("./", StringComparison.Ordinal))
            path = path[2..];
        if (path.Length == 0 || path.StartsWith('/') || path.Contains(':', StringComparison.Ordinal))
            return null;
        return path.Split('/').Any(s => s is "" or "." or "..") ? null : path;
    }
}
