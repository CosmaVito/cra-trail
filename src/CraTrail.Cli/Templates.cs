namespace CraTrail.Cli;

internal static class Templates
{
    public static string Read(string name)
    {
        using var stream = typeof(Templates).Assembly.GetManifestResourceStream("CraTrail.Cli.Templates." + name)
            ?? throw new InvalidOperationException($"Template {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
