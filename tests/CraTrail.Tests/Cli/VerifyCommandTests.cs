using System.IO.Compression;
using CraTrail.Cli;
using CraTrail.Cli.Attestation;

namespace CraTrail.Tests.Cli;

public class VerifyCommandTests
{
    private const string Workflow = "CosmaVito/cra-trail/.github/workflows/release.yml";

    [Fact]
    public void Hashes_are_checked_without_a_bundle()
    {
        using var run = Packed();

        Assert.Equal(ExitCodes.Ok, run.Run("verify", "out/pack.zip"));

        var lines = run.Out.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Matches("^manifest sha256 [0-9a-f]{64}$", lines[0]);
        Assert.Matches("^files: (\\d+) of \\1 hashes match$", lines[1]);
        Assert.Equal("signature: not checked, no --bundle given", lines[2]);
    }

    [Fact]
    public void Require_signature_without_a_bundle_exits_3()
    {
        using var run = Packed();
        Assert.Equal(ExitCodes.VerificationFailed, run.Run("verify", "out/pack.zip", "--require-signature"));
    }

    [Fact]
    public void A_bundle_needs_the_repository()
    {
        using var run = Packed();
        Assert.Equal(ExitCodes.InputError, run.Run("verify", "out/pack.zip", "--bundle", "manifest.sigstore.json"));
    }

    [Fact]
    public void A_verified_bundle_passes_and_the_workflow_reaches_the_verifier()
    {
        using var run = Packed();
        var stub = new StubVerifier(SignatureStatus.Verified);
        run.Verifier = stub;

        Assert.Equal(ExitCodes.Ok, run.Run("verify", "out/pack.zip", "--bundle", "manifest.sigstore.json", "--repo", "CosmaVito/cra-trail", "--signer-workflow", Workflow));

        Assert.Equal(("manifest.sigstore.json", "CosmaVito/cra-trail", (string?)Workflow), Assert.Single(stub.Calls));
        Assert.Contains("signature (manifest.sigstore.json): verified", run.Out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_invalid_bundle_exits_3()
    {
        using var run = Packed();
        run.Verifier = new StubVerifier(SignatureStatus.Invalid);

        Assert.Equal(ExitCodes.VerificationFailed, run.Run("verify", "out/pack.zip", "--bundle", "b.json", "--repo", "CosmaVito/cra-trail"));
        Assert.Contains("signature (b.json): INVALID, stub", run.Out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_gh_only_fails_when_a_signature_is_required()
    {
        using var run = Packed();
        Assert.Equal(ExitCodes.Ok, run.Run("verify", "out/pack.zip", "--bundle", "b.json", "--repo", "CosmaVito/cra-trail"));
        Assert.Contains("signature (b.json): not checked, gh CLI not found", run.Out.ToString(), StringComparison.Ordinal);
        Assert.Equal(ExitCodes.VerificationFailed, run.Run("verify", "out/pack.zip", "--bundle", "b.json", "--repo", "CosmaVito/cra-trail", "--require-signature"));
    }

    [Fact]
    public void Verify_fails_on_a_tampered_pack()
    {
        using var run = Packed();
        EvaluateCommandTests.Tamper(run.Path("out", "pack.zip"), "scans/grype.json");

        Assert.Equal(ExitCodes.VerificationFailed, run.Run("verify", "out/pack.zip"));
        Assert.Contains("problem: hash mismatch: scans/grype.json", run.Err.ToString(), StringComparison.Ordinal);
        Assert.Contains("files: verification failed", run.Out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_zip_with_an_unsafe_path_exits_3()
    {
        using var run = new CliRun();
        using (var file = File.Create(run.Path("evil.zip")))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("../evil.txt").Open());
            writer.Write("x");
        }

        Assert.Equal(ExitCodes.VerificationFailed, run.Run("verify", "evil.zip"));
        Assert.Contains("Unsafe path in pack: ../evil.txt", run.Err.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(run.Root)!, "evil.txt")));
    }

    private static CliRun Packed()
    {
        var run = new CliRun();
        CliPacks.WriteRepo(run);
        CliPacks.Pack(run);
        run.Out.GetStringBuilder().Clear();
        return run;
    }
}
