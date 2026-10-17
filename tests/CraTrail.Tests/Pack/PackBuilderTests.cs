using System.Text;
using System.Text.Json.Nodes;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Pack;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Pack;

public class PackBuilderTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 15, 8, 30, 0, TimeSpan.Zero);
    private static readonly byte[] Sbom = """{ "bomFormat": "CycloneDX", "specVersion": "1.6", "components": [] }"""u8.ToArray();
    private static readonly byte[] Vex = """
        { "@context": "https://openvex.dev/ns/v0.2.0", "@id": "https://example.org/vex/1", "author": "Example",
          "timestamp": "2026-10-01T00:00:00Z", "version": 1,
          "statements": [ { "vulnerability": { "name": "CVE-2021-44228" }, "status": "fixed" } ] }
        """u8.ToArray();

    [Fact]
    public void Places_each_input_by_kind_with_forward_slashes()
    {
        var pack = PackBuilder.Build(Request(
            new PackInput(EvidenceKinds.ProductSbom, "build\\product.cdx.json", Sbom),
            new PackInput(EvidenceKinds.Sbom, "build\\sbom\\backend.cdx.json", Sbom),
            Fixture(EvidenceKinds.Grype, "grype.json", "grype", "sample.json"),
            Fixture(EvidenceKinds.SastSarif, "codeql-csharp.sarif", "sarif", "codeql-extensions.sarif"),
            Fixture(EvidenceKinds.SecretsSarif, "leaks.sarif", "sarif", "betterleaks.sarif"),
            new PackInput(EvidenceKinds.CvdPolicy, "SECURITY.md", "# Security"u8.ToArray()),
            new PackInput(EvidenceKinds.TestResults, "out\\results.trx", "<TestRun />"u8.ToArray())),
            TestRules.Minimal, FakeEvaluate);

        Assert.Equal(new[]
        {
            "evaluation.json", "evidence/results.trx", "policy/SECURITY.md", "product.json",
            "sbom/backend.cdx.json", "sbom/product.cdx.json",
            "scans/codeql-csharp.sarif", "scans/grype.json", "scans/secrets.sarif",
        }, pack.Manifest.Files.Select(f => f.Path));
        Assert.All(pack.Manifest.Files, f => Assert.Equal(PackHashing.Sha256Hex(pack.Files[f.Path]), f.Sha256));
        Assert.All(pack.Manifest.Files, f => Assert.Equal(pack.Files[f.Path].LongLength, f.Size));
        Assert.Equal("application/vnd.cyclonedx+json", pack.Manifest.Files.Single(f => f.Path == "sbom/product.cdx.json").MediaType);
        Assert.Equal("2026-10-15T08:30:00Z", pack.Manifest.CreatedAt);
        Assert.Equal("test", pack.Manifest.RulesVersion);
        Assert.Equal("0.1.0", pack.Manifest.Generator.Version);
        Assert.Equal("CVD Inbox", pack.Manifest.Product.Name);
        Assert.Equal("CosmaVito/cra-trail", pack.Manifest.Source!.Repository);
    }

    [Fact]
    public void Secrets_sarif_is_redacted()
    {
        var pack = PackBuilder.Build(Request(
            new PackInput(EvidenceKinds.ProductSbom, "bom.json", Sbom),
            Fixture(EvidenceKinds.SecretsSarif, "leaks.sarif", "sarif", "betterleaks.sarif")), TestRules.Minimal, FakeEvaluate);

        var text = Encoding.UTF8.GetString(pack.Files["scans/secrets.sarif"]);
        Assert.DoesNotContain("not-a-real-key", text, StringComparison.Ordinal);
        Assert.DoesNotContain("partialFingerprints", text, StringComparison.Ordinal);
        Assert.Contains("REDACTED", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Vex_documents_are_merged_into_one_file()
    {
        var pack = PackBuilder.Build(Request(
            new PackInput(EvidenceKinds.ProductSbom, "bom.json", Sbom),
            new PackInput(EvidenceKinds.Vex, "a.openvex.json", Vex),
            new PackInput(EvidenceKinds.Vex, "b.openvex.json", Vex)), TestRules.Minimal, FakeEvaluate);

        var vex = Assert.Single(pack.Manifest.Files, f => f.Kind == EvidenceKinds.Vex);
        Assert.Equal("vex/product.openvex.json", vex.Path);
        var merged = JsonNode.Parse(pack.Files[vex.Path])!;
        Assert.Equal(2, merged["statements"]!.AsArray().Count);
        Assert.Equal("urn:cra-trail:vex:CVD%20Inbox:1.2.0", merged["@id"]!.GetValue<string>());
    }

    [Fact]
    public void Attested_documents_must_be_among_the_inputs()
    {
        var product = TestProducts.Default with { Attested = TestProducts.Map(("annex-i/part-i/1", "attested/docs/risk.md")) };
        var request = Request(new PackInput(EvidenceKinds.ProductSbom, "bom.json", Sbom)) with { Product = product };

        var error = Assert.Throws<PackBuildException>(() => PackBuilder.Build(request, TestRules.Minimal, FakeEvaluate));
        Assert.Equal("Attested document is missing from the inputs: attested/docs/risk.md", error.Message);

        var withDocument = request with
        {
            Inputs = [.. request.Inputs, new PackInput(EvidenceKinds.AttestedDocument, "risk.md", "# Risk"u8.ToArray(), "docs\\risk.md")],
        };
        Assert.Contains(
            PackBuilder.Build(withDocument, TestRules.Minimal, FakeEvaluate).Manifest.Files,
            f => f.Path == "attested/docs/risk.md" && f.Kind == EvidenceKinds.AttestedDocument);
    }

    [Fact]
    public void Evaluation_runs_on_the_pack_without_evaluation_json()
    {
        PackContents? seen = null;
        var pack = PackBuilder.Build(
            Request(new PackInput(EvidenceKinds.ProductSbom, "bom.json", Sbom)),
            TestRules.Minimal,
            (draft, rules) =>
            {
                seen = draft;
                return FakeEvaluate(draft, rules);
            });

        Assert.DoesNotContain(seen!.Manifest.Files, f => f.Path == PackContents.EvaluationPath);
        Assert.Contains(pack.Manifest.Files, f => f.Path == PackContents.EvaluationPath && f.Kind == EvidenceKinds.Evaluation);
        Assert.Equal("test", pack.ReadEvaluation()!.RulesVersion);
    }

    [Theory]
    [InlineData(EvidenceKinds.Grype, "grype.json", "{ \"matches\": [] }", "grype.json is not a valid grype input: ")]
    [InlineData(EvidenceKinds.ProductSbom, "bom.json", "{ \"bomFormat\": \"SPDX\" }", "bom.json is not a valid product-sbom input: ")]
    [InlineData(EvidenceKinds.SastSarif, "x.sarif", "not json", "x.sarif is not a valid sast-sarif input: ")]
    [InlineData(EvidenceKinds.Vex, "v.json", "{ }", "v.json is not a valid vex input: ")]
    [InlineData(EvidenceKinds.ProductSbom, "bom.json", "{ \"bomFormat\": \"CycloneDX\", \"specVersion\": \"1.5\" }",
        "bom.json is not a valid product-sbom input: Expected CycloneDX 1.6, found specVersion '1.5'.")]
    [InlineData(EvidenceKinds.Sbom, "backend.cdx.json", "{ \"bomFormat\": \"CycloneDX\" }",
        "backend.cdx.json is not a valid sbom input: Expected CycloneDX 1.6, found specVersion 'none'.")]
    public void Invalid_inputs_are_rejected(string kind, string fileName, string content, string expectedStart)
    {
        var request = Request(
            new PackInput(EvidenceKinds.ProductSbom, "product.cdx.json", Sbom),
            new PackInput(kind, fileName, Encoding.UTF8.GetBytes(content)));
        var error = Assert.Throws<PackBuildException>(() => PackBuilder.Build(request, TestRules.Minimal, FakeEvaluate));
        Assert.StartsWith(expectedStart, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_kinds_and_clashing_paths_are_rejected()
    {
        Assert.Equal(
            "Unknown evidence kind 'binary'.",
            Assert.Throws<PackBuildException>(() => PackBuilder.Build(
                Request(new PackInput("binary", "a.bin", [1])), TestRules.Minimal, FakeEvaluate)).Message);
        Assert.Equal(
            "Two inputs map to the same pack path: sbom/product.cdx.json",
            Assert.Throws<PackBuildException>(() => PackBuilder.Build(
                Request(new PackInput(EvidenceKinds.ProductSbom, "a.json", Sbom), new PackInput(EvidenceKinds.ProductSbom, "b.json", Sbom)),
                TestRules.Minimal, FakeEvaluate)).Message);
    }

    [Fact]
    public void Reevaluate_replaces_only_the_evaluation()
    {
        var pack = PackBuilder.Build(Request(new PackInput(EvidenceKinds.ProductSbom, "bom.json", Sbom)), TestRules.Minimal, FakeEvaluate);
        var newer = RuleSetLoader.Load("version: \"v2\"\ngate: []\nchecks: {}\nrequirements: []\n", []);

        var again = PackBuilder.Reevaluate(pack, newer, FakeEvaluate);

        Assert.Equal("v2", again.Manifest.RulesVersion);
        Assert.Equal("v2", again.ReadEvaluation()!.RulesVersion);
        Assert.Equal(pack.Manifest.CreatedAt, again.Manifest.CreatedAt);
        Assert.Equal(pack.Files["sbom/product.cdx.json"], again.Files["sbom/product.cdx.json"]);
        Assert.NotEqual(pack.Files[PackContents.EvaluationPath], again.Files[PackContents.EvaluationPath]);
    }

    [Fact]
    public void A_file_name_with_a_colon_is_refused_before_it_reaches_the_pack()
    {
        var request = Request(
            new PackInput(EvidenceKinds.ProductSbom, "product.cdx.json", Sbom),
            Fixture(EvidenceKinds.SastSarif, "codeql:cs.sarif", "sarif", "codeql-driver.sarif"));

        var error = Assert.Throws<PackBuildException>(() => PackBuilder.Build(request, TestRules.Minimal, FakeEvaluate));

        Assert.Equal("'codeql:cs.sarif' cannot be used as a file name in the pack. Rename it without ':'.", error.Message);
    }

    [Fact]
    public void Static_analysis_sarif_is_redacted_too()
    {
        var pack = PackBuilder.Build(Request(
            new PackInput(EvidenceKinds.ProductSbom, "bom.json", Sbom),
            Fixture(EvidenceKinds.SastSarif, "leaks.sarif", "sarif", "betterleaks.sarif")), TestRules.Minimal, FakeEvaluate);

        var text = Encoding.UTF8.GetString(pack.Files["scans/leaks.sarif"]);
        Assert.DoesNotContain("not-a-real-key", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dev@example.org", text, StringComparison.Ordinal);
    }

    private static PackRequest Request(params PackInput[] inputs) => new(
        TestProducts.Default,
        inputs,
        [new ToolInfo("grype", "0.120.1")],
        new SourceInfo("CosmaVito/cra-trail", "abc123", "refs/tags/v0.1.0", null, "42"),
        new SigningInfo("github-artifact-attestations", "CosmaVito/cra-trail/.github/workflows/release.yml"),
        CreatedAt,
        "0.1.0");

    private static PackInput Fixture(string kind, string fileName, params string[] fixture) =>
        new(kind, fileName, File.ReadAllBytes(TestPaths.Fixture(fixture)));

    private static EvaluationResult FakeEvaluate(PackContents pack, RuleSet rules) =>
        new("0.1", rules.Version, [], [], new GateOutcome(true, [], []), new StatusTotals(0, 0, 0, 0, 0));
}
