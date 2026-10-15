namespace CraTrail.Cli;

internal sealed record CliIO(TextWriter Out, TextWriter Err, Func<string, string?> GetEnvironmentVariable, string WorkingDirectory)
{
    public static CliIO FromProcess() =>
        new(Console.Out, Console.Error, Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory());

    public string Resolve(string path) => Path.GetFullPath(Path.Combine(WorkingDirectory, path));
}
