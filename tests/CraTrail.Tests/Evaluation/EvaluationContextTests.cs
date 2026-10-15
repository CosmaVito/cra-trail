using CraTrail.Core.Evaluation;
using CraTrail.Core.Pack;

namespace CraTrail.Tests.Evaluation;

public class EvaluationContextTests
{
    private const string Vex = """
        { "@context": "https://openvex.dev/ns/v0.2.0", "@id": "https://example.org/vex/1", "author": "Example",
          "timestamp": "2026-10-01T00:00:00Z", "version": 1,
          "statements": [ { "vulnerability": { "name": "CVE-2021-44228" }, "status": "not_affected", "justification": "component_not_present" } ] }
        """;

    [Fact]
    public void Reads_findings_vex_and_sarif_by_kind()
    {
        var context = new TestPackBuilder()
            .AddFixture("scans/grype.json", EvidenceKinds.Grype, "grype", "sample.json")
            .Add("vex/product.openvex.json", EvidenceKinds.Vex, Vex)
            .AddFixture("scans/codeql.sarif", EvidenceKinds.SastSarif, "sarif", "codeql-driver.sarif")
            .WithSigning("github-artifact-attestations")
            .Context();

        Assert.Equal(3, context.Findings.Count);
        Assert.Single(context.Vex.Statements);
        Assert.Equal(2, context.SarifResults(EvidenceKinds.SastSarif).Count);
        Assert.Empty(context.SarifResults(EvidenceKinds.SecretsSarif));
        Assert.Equal("CVD Inbox", context.ProductJson["name"]!.GetValue<string>());
        Assert.Equal("github-artifact-attestations", context.ManifestJson["signing"]!["method"]!.GetValue<string>());
        Assert.Single(context.FilesOfKind(EvidenceKinds.Grype));
    }

    [Fact]
    public void Broken_evidence_names_the_file()
    {
        var context = new TestPackBuilder().Add("scans/grype.json", EvidenceKinds.Grype, "not json").Context();
        var error = Assert.Throws<EvidenceFormatException>(() => context.Findings);
        Assert.Equal("scans/grype.json", error.Path);
    }

    [Fact]
    public void Pack_contents_need_product_json_and_every_listed_file()
    {
        var pack = new TestPackBuilder().Add("sbom/product.cdx.json", EvidenceKinds.ProductSbom, "{}").Build();

        var withoutSbom = pack.Files.Where(f => f.Key != "sbom/product.cdx.json").ToDictionary(f => f.Key, f => f.Value);
        Assert.Throws<PackFormatException>(() => new PackContents(pack.Manifest, pack.ManifestBytes, withoutSbom));

        var extra = new Dictionary<string, byte[]>(pack.Files) { ["extra.txt"] = [1] };
        Assert.Throws<PackFormatException>(() => new PackContents(pack.Manifest, pack.ManifestBytes, extra));

        var manifest = pack.Manifest with { Files = [.. pack.Manifest.Files.Where(f => f.Path != PackContents.ProductPath)] };
        var files = pack.Files.Where(f => f.Key != PackContents.ProductPath).ToDictionary(f => f.Key, f => f.Value);
        Assert.Throws<PackFormatException>(() => PackContents.Create(manifest, files));
    }

    [Fact]
    public void Failing_details_are_sorted_and_unique()
    {
        var check = TestChecks.Define("c", "type: file-present\nkind: product-sbom");
        Assert.Equal(new[] { "a", "b" }, CheckOutcome.Fail(check, ["b", "a", "b"]).Details);
        Assert.Equal(Statuses.Fail, CheckOutcome.Fail(check, ["x"]).Status);
        Assert.Equal(new[] { "no file" }, CheckOutcome.Missing(check, "no file").Details);
        Assert.Empty(CheckOutcome.Pass(check).Details);
    }
}
