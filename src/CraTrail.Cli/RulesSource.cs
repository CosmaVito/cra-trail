using CraTrail.Core.Rules;

namespace CraTrail.Cli;

internal static class RulesSource
{
    public static RuleSet Load(string? directory, CliIO io)
    {
        if (directory is null)
            return DefaultRules.Load();

        var root = io.Resolve(directory);
        var crosswalk = Path.Combine(root, "crosswalk.yaml");
        if (!File.Exists(crosswalk))
            throw new IOException($"No crosswalk.yaml in {directory}.");
        var profiles = Directory.GetFiles(root, "*.yaml")
            .Where(f => Path.GetFileName(f) != "crosswalk.yaml")
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
        return RuleSetLoader.Load(File.ReadAllText(crosswalk), profiles);
    }
}
