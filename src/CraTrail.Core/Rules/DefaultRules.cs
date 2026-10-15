namespace CraTrail.Core.Rules;

public static class DefaultRules
{
    private const string Prefix = "CraTrail.Rules.";
    private const string Crosswalk = Prefix + "crosswalk.yaml";

    public static RuleSet Load()
    {
        var assembly = typeof(DefaultRules).Assembly;
        var profiles = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n != Crosswalk)
            .Order(StringComparer.Ordinal)
            .Select(Read)
            .ToList();
        return RuleSetLoader.Load(Read(Crosswalk), profiles);
    }

    private static string Read(string name)
    {
        using var stream = typeof(DefaultRules).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded rules file {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
