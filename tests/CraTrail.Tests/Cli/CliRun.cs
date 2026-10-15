using CraTrail.Cli;
using CraTrail.Cli.Attestation;

namespace CraTrail.Tests.Cli;

internal sealed class CliRun : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("cra-trail-test-").FullName;

    public StringWriter Out { get; } = new() { NewLine = "\n" };

    public StringWriter Err { get; } = new() { NewLine = "\n" };

    public Dictionary<string, string?> Environment { get; } = new(StringComparer.Ordinal);

    public IAttestationVerifier Verifier { get; set; } = new StubVerifier(SignatureStatus.Unavailable);

    public int Run(params string[] args) =>
        CliApp.Run(args, new CliIO(Out, Err, name => Environment.GetValueOrDefault(name), Root), Verifier);

    public string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    public void Dispose()
    {
        Out.Dispose();
        Err.Dispose();
        Directory.Delete(Root, recursive: true);
    }
}

internal sealed class StubVerifier(SignatureStatus status) : IAttestationVerifier
{
    public List<(string Bundle, string Repository, string? SignerWorkflow)> Calls { get; } = [];

    public SignatureResult Verify(byte[] manifestBytes, string bundlePath, string repository, string? signerWorkflow)
    {
        Calls.Add((Path.GetFileName(bundlePath), repository, signerWorkflow));
        return new SignatureResult(status, status == SignatureStatus.Unavailable ? "gh CLI not found" : "stub");
    }
}
