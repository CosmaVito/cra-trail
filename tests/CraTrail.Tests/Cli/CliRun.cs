using CraTrail.Cli;

namespace CraTrail.Tests.Cli;

internal sealed class CliRun : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("cra-trail-test-").FullName;

    public StringWriter Out { get; } = new() { NewLine = "\n" };

    public StringWriter Err { get; } = new() { NewLine = "\n" };

    public Dictionary<string, string?> Environment { get; } = new(StringComparer.Ordinal);

    public int Run(params string[] args) =>
        CliApp.Run(args, new CliIO(Out, Err, name => Environment.GetValueOrDefault(name), Root));

    public string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    public void Dispose()
    {
        Out.Dispose();
        Err.Dispose();
        Directory.Delete(Root, recursive: true);
    }
}
