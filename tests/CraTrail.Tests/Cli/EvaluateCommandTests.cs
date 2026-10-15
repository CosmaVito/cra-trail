using System.IO.Compression;
using System.Text.Json.Nodes;
using CraTrail.Cli;
using CraTrail.Core.Pack;

namespace CraTrail.Tests.Cli;

public class EvaluateCommandTests
{
    [Fact]
    public void Re_evaluates_with_other_rules()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);
        CliPacks.Pack(run);
        var rules = run.Path("rules-v2");
        Directory.CreateDirectory(rules);
        foreach (var file in Directory.GetFiles(TestPaths.Conformance("rules")))
            File.Copy(file, Path.Combine(rules, Path.GetFileName(file)));
        File.WriteAllText(Path.Combine(rules, "crosswalk.yaml"),
            File.ReadAllText(Path.Combine(rules, "crosswalk.yaml")).Replace("version: \"test-1\"", "version: \"test-2\"", StringComparison.Ordinal));

        Assert.Equal(ExitCodes.Ok, run.Run("evaluate", "out/pack.zip", "--rules", rules));

        Assert.Contains("rules test-2, gate passed", run.Out.ToString(), StringComparison.Ordinal);
        using var stream = File.OpenRead(run.Path("out", "pack.zip"));
        var entries = PackArchive.Read(stream);
        Assert.Equal("test-2", JsonNode.Parse(entries[PackManifest.FileName])!["rulesVersion"]!.GetValue<string>());
        Assert.True(PackVerifier.Verify(entries).Ok);
    }

    [Fact]
    public void A_tampered_pack_is_refused()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);
        CliPacks.Pack(run);
        Tamper(run.Path("out", "pack.zip"), "sbom/product.cdx.json");

        Assert.Equal(ExitCodes.VerificationFailed, run.Run("evaluate", "out/pack.zip"));
        Assert.Contains("problem: hash mismatch: sbom/product.cdx.json", run.Err.ToString(), StringComparison.Ordinal);
    }

    internal static void Tamper(string zipPath, string entryName)
    {
        IReadOnlyDictionary<string, byte[]> entries;
        using (var input = File.OpenRead(zipPath))
            entries = PackArchive.Read(input);
        using var output = File.Create(zipPath);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (var (name, bytes) in entries)
        {
            var content = name == entryName ? bytes.Append((byte)' ').ToArray() : bytes;
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(content);
        }
    }
}
