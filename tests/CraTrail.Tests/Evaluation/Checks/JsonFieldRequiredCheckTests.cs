using CraTrail.Core.Evaluation;
using CraTrail.Core.Evaluation.Checks;
using CraTrail.Core.Pack;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Evaluation.Checks;

public class JsonFieldRequiredCheckTests
{
    private static readonly CheckDefinition Core = TestChecks.Define("sbom-core-fields", "type: json-field-required\nkind: product-sbom\nprofile: sbom-core");
    private static readonly CheckDefinition Bsi = TestChecks.Define("sbom-bsi-fields", "type: json-field-required\nkind: product-sbom\nprofile: bsi-mini");
    private static readonly CheckDefinition Custom = TestChecks.Define("c", "type: json-field-required\nkind: product-sbom\nprofile: p");

    [Fact]
    public void Is_missing_without_a_file_of_the_kind()
    {
        var outcome = Evaluate(Core, new TestPackBuilder(), TestRules.Conformance);
        Assert.Equal(Statuses.Missing, outcome.Status);
        Assert.Equal(new[] { "no file of kind product-sbom" }, outcome.Details);
    }

    [Fact]
    public void Passes_on_a_complete_sbom()
    {
        Assert.Equal(Statuses.Pass, Evaluate(Core, Sbom("sbom-clean.cdx.json"), TestRules.Conformance).Status);
        Assert.Equal(Statuses.Pass, Evaluate(Bsi, Sbom("sbom-clean.cdx.json"), TestRules.Conformance).Status);
    }

    [Fact]
    public void Names_each_component_that_misses_a_field_and_skips_assemblies() =>
        Assert.Equal(new[] { "component-version: ref-a" }, Evaluate(Core, Sbom("sbom-core-missing.cdx.json"), TestRules.Conformance).Details);

    [Fact]
    public void Lists_root_and_component_fields_in_order() =>
        Assert.Equal(new[]
        {
            "component-executable: cvd-inbox", "component-executable: ref-angular-core", "component-executable: ref-newtonsoft",
            "component-filename: cvd-inbox", "component-filename: ref-angular-core", "component-filename: ref-newtonsoft",
            "sbom-creator",
        }, Evaluate(Bsi, Sbom("sbom-no-bsi.cdx.json"), TestRules.Conformance).Details);

    [Theory]
    [InlineData("a.b", "present", """{ "a": { "b": "" } }""", true)]
    [InlineData("a.b", "present", """{ "a": { "b": null } }""", false)]
    [InlineData("a.b", "non-empty", """{ "a": { "b": "  " } }""", false)]
    [InlineData("a.b", "non-empty", """{ "a": { "b": [] } }""", false)]
    [InlineData("a.b", "non-empty", """{ "a": { "b": 0 } }""", true)]
    [InlineData("a.b", "absent", """{ "a": { } }""", true)]
    [InlineData("a.b", "absent", """{ "a": { "b": "x" } }""", false)]
    [InlineData("a.b[*]", "{ contains: SHA-512 }", """{ "a": { "b": ["SHA-256", "SHA-512"] } }""", true)]
    [InlineData("a.b[*]", "{ contains: SHA-512 }", """{ "a": { "b": ["SHA-256"] } }""", false)]
    [InlineData("a.b[*]", "{ one-of: [x, y] }", """{ "a": { "b": ["x", "y"] } }""", true)]
    [InlineData("a.b[*]", "{ one-of: [x, y] }", """{ "a": { "b": ["x", "z"] } }""", false)]
    [InlineData("a.b[*]", "{ one-of: [x, y] }", """{ "a": { "b": [] } }""", false)]
    public void Requirement_kinds(string path, string require, string json, bool passes)
    {
        var rules = TestRules.WithProfile($"id: p\ntitle: P\nfields:\n  - id: f\n    path: {path}\n    require: {require}\n");
        var outcome = Evaluate(Custom, new TestPackBuilder().Add("sbom/product.cdx.json", EvidenceKinds.ProductSbom, json), rules);
        Assert.Equal(passes ? Statuses.Pass : Statuses.Fail, outcome.Status);
        Assert.Equal(passes ? Array.Empty<string>() : new[] { "f" }, outcome.Details);
    }

    [Fact]
    public void Components_are_named_by_bom_ref_purl_name_or_position()
    {
        var rules = TestRules.WithProfile("id: p\ntitle: P\nfields:\n  - id: version\n    each: components\n    path: version\n    require: non-empty\n");
        var sbom = """{ "components": [ { "bom-ref": "r" }, { "purl": "pkg:npm/x" }, { "name": "n" }, { } ] }""";
        var outcome = Evaluate(Custom, new TestPackBuilder().Add("sbom/product.cdx.json", EvidenceKinds.ProductSbom, sbom), rules);
        Assert.Equal(new[] { "version: #3", "version: n", "version: pkg:npm/x", "version: r" }, outcome.Details);
    }

    [Fact]
    public void Broken_evidence_fails_with_the_file_name() =>
        Assert.Equal(
            new[] { "invalid evidence: sbom/product.cdx.json" },
            Evaluate(Core, new TestPackBuilder().Add("sbom/product.cdx.json", EvidenceKinds.ProductSbom, "{"), TestRules.Conformance).Details);

    private static TestPackBuilder Sbom(string name) =>
        new TestPackBuilder().AddShared("sbom/product.cdx.json", EvidenceKinds.ProductSbom, name);

    private static CheckOutcome Evaluate(CheckDefinition check, TestPackBuilder pack, RuleSet rules) =>
        new JsonFieldRequiredCheck().Evaluate(check, pack.Context(rules));
}
