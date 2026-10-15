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

internal sealed class GhAttestationVerifier : IAttestationVerifier
{
    public SignatureResult Verify(byte[] manifestBytes, string bundlePath, string repository, string? signerWorkflow)
    {
        var folder = Directory.CreateTempSubdirectory("cra-trail-");
        try
        {
            var manifestPath = Path.Combine(folder.FullName, "manifest.json");
            File.WriteAllBytes(manifestPath, manifestBytes);

            var start = new ProcessStartInfo("gh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[] { "attestation", "verify", manifestPath, "--bundle", bundlePath, "--repo", repository })
                start.ArgumentList.Add(argument);
            if (signerWorkflow is not null)
            {
                start.ArgumentList.Add("--signer-workflow");
                start.ArgumentList.Add(signerWorkflow);
            }

            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start gh.");
            var errors = process.StandardError.ReadToEndAsync();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            var message = errors.GetAwaiter().GetResult().Trim();
            return process.ExitCode == 0
                ? new SignatureResult(SignatureStatus.Verified, "verified by gh attestation verify")
                : new SignatureResult(SignatureStatus.Invalid, message.Length > 0 ? message : $"gh exited with code {process.ExitCode}");
        }
        catch (Win32Exception)
        {
            return new SignatureResult(SignatureStatus.Unavailable, "gh CLI not found");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
