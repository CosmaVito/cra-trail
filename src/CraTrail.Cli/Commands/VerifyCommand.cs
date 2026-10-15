using System.CommandLine;
using CraTrail.Cli.Attestation;

namespace CraTrail.Cli.Commands;

internal sealed record VerifyOptions(
    string Pack,
    IReadOnlyList<string> Bundles,
    string? Repository,
    string? SignerWorkflow,
    bool RequireSignature);

internal static class VerifyCommand
{
    public static Command Create(CliIO io, IAttestationVerifier verifier)
    {
        var pack = new Argument<string>("pack") { Description = "The evidence pack zip." };
        var bundles = new Option<string[]>("--bundle") { Description = "Sigstore bundle (.sigstore.json) that attests the manifest." };
        var repository = new Option<string?>("--repo") { Description = "Repository that built the pack, as owner/name. Needed with --bundle." };
        var signerWorkflow = new Option<string?>("--signer-workflow") { Description = "Workflow that must have signed the bundle." };
        var requireSignature = new Option<bool>("--require-signature") { Description = "Fail when the signature cannot be checked." };
        var command = new Command("verify", "Check the hashes of an evidence pack and, with --bundle, its signature.")
        {
            pack, bundles, repository, signerWorkflow, requireSignature,
        };
        command.SetAction(parse => Guard.Run(io, () => Run(io, verifier, new VerifyOptions(
            parse.GetValue(pack)!,
            parse.GetValue(bundles) ?? [],
            parse.GetValue(repository),
            parse.GetValue(signerWorkflow),
            parse.GetValue(requireSignature)))));
        return command;
    }

    internal static int Run(CliIO io, IAttestationVerifier verifier, VerifyOptions options)
    {
        if (options.Bundles.Count > 0 && string.IsNullOrWhiteSpace(options.Repository))
        {
            io.Err.WriteLine("error: pass --repo together with --bundle.");
            return ExitCodes.InputError;
        }

        var (pack, result) = PackLoader.Load(io, options.Pack);
        io.Out.WriteLine($"manifest sha256 {result.ManifestSha256}");
        if (pack is null)
        {
            io.Out.WriteLine("files: verification failed");
            return ExitCodes.VerificationFailed;
        }
        io.Out.WriteLine($"files: {pack.Manifest.Files.Count} of {pack.Manifest.Files.Count} hashes match");

        if (options.Bundles.Count == 0)
        {
            io.Out.WriteLine("signature: not checked, no --bundle given");
            return options.RequireSignature ? ExitCodes.VerificationFailed : ExitCodes.Ok;
        }

        var exit = ExitCodes.Ok;
        foreach (var bundle in options.Bundles)
        {
            var signature = verifier.Verify(pack.ManifestBytes, io.Resolve(bundle), options.Repository!, options.SignerWorkflow);
            io.Out.WriteLine($"signature ({Path.GetFileName(bundle)}): {Describe(signature)}");
            if (signature.Status == SignatureStatus.Invalid
                || (signature.Status == SignatureStatus.Unavailable && options.RequireSignature))
                exit = ExitCodes.VerificationFailed;
        }
        return exit;
    }

    private static string Describe(SignatureResult result) => result.Status switch
    {
        SignatureStatus.Verified => "verified",
        SignatureStatus.Invalid => $"INVALID, {result.Message}",
        _ => $"not checked, {result.Message}",
    };
}
