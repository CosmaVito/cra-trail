using CraTrail.Core.Rules;

namespace CraTrail.Tests;

internal static class TestRules
{
    private static readonly Lazy<RuleSet> ConformanceRules = new(() => LoadDirectory(TestPaths.Conformance("rules")));

    public static RuleSet Conformance => ConformanceRules.Value;

    public static RuleSet Minimal { get; } = RuleSetLoader.Load("version: \"test\"\ngate: []\nchecks: {}\nrequirements: []\n", []);

    public static RuleSet LoadDirectory(string directory)
    {
        var profiles = Directory.GetFiles(directory, "*.yaml")
            .Where(f => Path.GetFileName(f) != "crosswalk.yaml")
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
        return RuleSetLoader.Load(File.ReadAllText(Path.Combine(directory, "crosswalk.yaml")), profiles);
    }

    public static RuleSet WithProfile(string profileYaml) =>
        RuleSetLoader.Load("version: \"test\"\ngate: []\nchecks: {}\nrequirements: []\n", [profileYaml]);
}
