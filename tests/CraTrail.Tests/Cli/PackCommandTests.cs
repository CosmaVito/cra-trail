using System.Text;
using System.Text.Json.Nodes;
using CraTrail.Cli;
using CraTrail.Core.Pack;

namespace CraTrail.Tests.Cli;

public class PackCommandTests
{
    [Fact]
    public void Builds_a_pack_whose_evaluation_matches_the_clean_vector()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);

        Assert.Equal(ExitCodes.Ok, CliPacks.Pack(run));

        Assert.Contains("gate: passed", run.Out.ToString(), StringComparison.Ordinal);
        var entries = Read(run);
        Assert.Contains("attested/docs/risk-assessment.md", entries.Keys);
        Assert.Contains("scans/sast-clean.sarif", entries.Keys);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(File.ReadAllBytes(TestPaths.Conformance("cases", "clean", "expected.json"))),
            JsonNode.Parse(entries[PackContents.EvaluationPath])));
        Assert.True(PackVerifier.Verify(entries).Ok);
    }

    [Fact]
    public void The_same_inputs_give_the_same_bytes()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);

        CliPacks.Pack(run);
        var first = File.ReadAllBytes(run.Path("out", "pack.zip"));
        CliPacks.Pack(run);

        Assert.Equal(first, File.ReadAllBytes(run.Path("out", "pack.zip")));
    }

    [Fact]
    public void The_github_environment_becomes_the_pack_source()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);
        run.Environment["GITHUB_REPOSITORY"] = "CosmaVito/cra-trail";
        run.Environment["GITHUB_SHA"] = "abc123";
        run.Environment["GITHUB_RUN_ID"] = "42";

        CliPacks.Pack(run);

        var manifest = JsonNode.Parse(Read(run)[PackManifest.FileName])!;
        Assert.Equal("CosmaVito/cra-trail", manifest["source"]!["repository"]!.GetValue<string>());
        Assert.Equal("abc123", manifest["source"]!["commit"]!.GetValue<string>());
        Assert.Equal("github-artifact-attestations", manifest["signing"]!["method"]!.GetValue<string>());
    }

    [Fact]
    public void Secrets_are_redacted_in_the_pack()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);

        CliPacks.Pack(run, secrets: TestPaths.Fixture("sarif", "betterleaks.sarif"));

        Assert.DoesNotContain("not-a-real-key", Encoding.UTF8.GetString(Read(run)["scans/secrets.sarif"]), StringComparison.Ordinal);
    }

    [Fact]
    public void A_blocked_gate_still_writes_the_pack()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);

        Assert.Equal(ExitCodes.Ok, CliPacks.Pack(run, "grype-kev.json"));

        Assert.Contains("gate: blocked by no-kev-without-vex, no-critical-without-vex", run.Out.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(run.Path("out", "pack.zip")));
    }

    [Fact]
    public void Configuration_errors_exit_with_2()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run, CliPacks.Config + "\nowner: me\n");

        Assert.Equal(ExitCodes.InputError, CliPacks.Pack(run));
        Assert.Contains("unknown key 'owner'", run.Err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_version_comes_from_the_sbom_or_the_option()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);
        string[] args =
        [
            "pack", "--product-sbom", TestPaths.Conformance("shared", "sbom-core-missing.cdx.json"),
            "--rules", TestPaths.Conformance("rules"), "--out", "out/pack.zip",
        ];

        Assert.Equal(ExitCodes.InputError, run.Run(args));
        Assert.Contains("Pass --product-version", run.Err.ToString(), StringComparison.Ordinal);

        Assert.Equal(ExitCodes.Ok, run.Run([.. args, "--product-version", "2.0.0"]));
        Assert.Equal("2.0.0", JsonNode.Parse(Read(run)[PackManifest.FileName])!["product"]!["version"]!.GetValue<string>());
    }

    [Fact]
    public void Unknown_evidence_kinds_exit_with_2()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);

        Assert.Equal(ExitCodes.InputError, CliPacks.Pack(run, "grype-clean.json", null, "--evidence", "binary=" + run.Path("cra.yml")));
        Assert.Contains("--evidence: unknown kind 'binary'", run.Err.ToString(), StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, byte[]> Read(CliRun run)
    {
        using var stream = File.OpenRead(run.Path("out", "pack.zip"));
        return PackArchive.Read(stream);
    }
}
