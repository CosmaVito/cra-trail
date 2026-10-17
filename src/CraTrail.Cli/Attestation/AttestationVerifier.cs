using System.ComponentModel;
using System.Diagnostics;

namespace CraTrail.Cli.Attestation;

internal enum SignatureStatus
{
    Verified,
    Invalid,
    Unavailable,
}

internal sealed record SignatureResult(SignatureStatus Status, string Message);

internal interface IAttestationVerifier
{
    SignatureResult Verify(byte[] manifestBytes, string bundlePath, string repository, string? signerWorkflow);
}

internal sealed record GhRun(int ExitCode, string Output, string Error, bool TimedOut);

internal interface IGhRunner
{
    // Throws Win32Exception when gh is not installed.
    GhRun Run(IReadOnlyList<string> arguments, TimeSpan timeout);
}

internal sealed class GhProcessRunner : IGhRunner
{
    public GhRun Run(IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var start = new ProcessStartInfo("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start gh.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return new GhRun(-1, "", "", TimedOut: true);
        }
        return new GhRun(process.ExitCode, output.GetAwaiter().GetResult(), errors.GetAwaiter().GetResult().Trim(), TimedOut: false);
    }
}

// Two steps, so that a network problem never reads as a bad signature. Only the first one, which fetches
// Sigstore's trusted root through TUF, uses the network. The second one verifies offline against that root.
// Neither asks for a gh login.
internal sealed class GhAttestationVerifier(IGhRunner gh) : IAttestationVerifier
{
    // The release workflow attests the manifest with this predicate; gh looks for SLSA provenance unless told otherwise.
    public const string EvaluationPredicate = "https://cosmavito.github.io/cra-trail/predicates/evaluation/v0.1";

    private const int TimeoutSeconds = 60;

    // gh help exit-codes: 4 means the command needs authentication.
    private const int NeedsAuthentication = 4;

    public GhAttestationVerifier()
        : this(new GhProcessRunner())
    {
    }

    public SignatureResult Verify(byte[] manifestBytes, string bundlePath, string repository, string? signerWorkflow)
    {
        var folder = Directory.CreateTempSubdirectory("cra-trail-");
        try
        {
            var manifestPath = Path.Combine(folder.FullName, "manifest.json");
            File.WriteAllBytes(manifestPath, manifestBytes);

            var root = gh.Run(["attestation", "trusted-root"], TimeSpan.FromSeconds(TimeoutSeconds));
            if (Failure(root, "trusted-root") is { } rootFailure)
                return rootFailure;
            if (root.ExitCode != 0)
                return Unavailable($"could not fetch the Sigstore trusted root: {Message(root)}");
            var rootPath = Path.Combine(folder.FullName, "trusted_root.jsonl");
            File.WriteAllText(rootPath, root.Output);

            var check = gh.Run(Arguments(manifestPath, bundlePath, repository, signerWorkflow, rootPath), TimeSpan.FromSeconds(TimeoutSeconds));
            if (Failure(check, "verify") is { } checkFailure)
                return checkFailure;
            return check.ExitCode == 0
                ? new SignatureResult(SignatureStatus.Verified, "verified by gh attestation verify")
                : new SignatureResult(SignatureStatus.Invalid, Message(check));
        }
        catch (Win32Exception)
        {
            return Unavailable("gh CLI not found");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    internal static List<string> Arguments(string manifestPath, string bundlePath, string repository, string? signerWorkflow, string trustedRootPath)
    {
        List<string> arguments =
        [
            "attestation", "verify", manifestPath, "--bundle", bundlePath, "--repo", repository,
            "--predicate-type", EvaluationPredicate,
            "--custom-trusted-root", trustedRootPath,
        ];
        if (signerWorkflow is not null)
            arguments.AddRange(["--signer-workflow", signerWorkflow]);
        return arguments;
    }

    // Results that say nothing about the bundle: the step did not finish, or gh wants a login.
    private static SignatureResult? Failure(GhRun run, string step) =>
        run.TimedOut ? Unavailable($"gh attestation {step} did not finish within {TimeoutSeconds} s")
        : run.ExitCode == NeedsAuthentication ? Unavailable("gh needs a login: run gh auth login")
        : null;

    private static SignatureResult Unavailable(string message) => new(SignatureStatus.Unavailable, message);

    private static string Message(GhRun run) => run.Error.Length > 0 ? run.Error : $"gh exited with code {run.ExitCode}";
}
