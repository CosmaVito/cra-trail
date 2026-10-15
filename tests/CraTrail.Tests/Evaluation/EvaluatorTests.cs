using System.Text.Json.Nodes;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Evaluation.Checks;
using CraTrail.Core.Pack;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Evaluation;

public class EvaluatorTests
{
    [Fact]
    public void Evaluates_every_check_once_sorted_by_id()
    {
        var rules = RuleSetLoader.Load("""
            version: "t"
            gate: []
            checks:
              b-check:
                type: file-present
                kind: product-sbom
              a-check:
                type: file-present
                kind: grype
            requirements: []
            """, []);

        var result = Evaluator.CreateDefault().Evaluate(new TestPackBuilder().Build(), rules);

        Assert.Equal(new[] { "a-check", "b-check" }, result.Checks.Select(c => c.Id));
        Assert.Equal(EvaluationResult.CurrentSchemaVersion, result.SchemaVersion);
        Assert.Equal("t", result.RulesVersion);
        Assert.Equal(new StatusTotals(0, 0, 0, 0, 0), result.Totals);
    }

    [Fact]
    public void Product_overrides_change_check_parameters()
    {
        var product = TestProducts.Default with
        {
            CheckOverrides = TestProducts.Map(("no-critical-without-vex", new JsonObject { ["select"] = new JsonObject { ["min-cvss"] = 7.0 } })),
        };
        var pack = new TestPackBuilder().WithProduct(product).AddShared("scans/grype.json", EvidenceKinds.Grype, "grype-high.json").Build();

        var result = Evaluator.CreateDefault().Evaluate(pack, TestRules.Conformance);

        Assert.Equal(Statuses.Fail, result.Checks.Single(c => c.Id == "no-critical-without-vex").Status);
    }

    [Fact]
    public void An_attested_requirement_needs_its_document_in_the_pack()
    {
        var product = TestProducts.Default with { Attested = TestProducts.Map(("t/risk", "attested/docs/risk.md")) };
        var withoutDocument = Evaluator.CreateDefault().Evaluate(new TestPackBuilder().WithProduct(product).Build(), TestRules.Conformance);
        var withDocument = Evaluator.CreateDefault().Evaluate(
            new TestPackBuilder().WithProduct(product).Add("attested/docs/risk.md", EvidenceKinds.AttestedDocument, "# Risk").Build(),
            TestRules.Conformance);

        var missing = withoutDocument.Requirements.Single(r => r.Id == "t/risk");
        Assert.Equal(Statuses.Missing, missing.Status);
        Assert.Null(missing.Attestation);
        Assert.Equal(new[] { "attested document not in pack: attested/docs/risk.md" }, missing.Details);

        var attested = withDocument.Requirements.Single(r => r.Id == "t/risk");
        Assert.Equal(Statuses.Attested, attested.Status);
        Assert.Equal("attested/docs/risk.md", attested.Attestation);
    }

    [Fact]
    public void An_unknown_check_type_is_an_error()
    {
        var evaluator = new Evaluator([new FilePresentCheck()]);
        var rules = RuleSetLoader.Load("version: \"t\"\ngate: []\nchecks:\n  c:\n    type: attested-field\n    field: product.name\nrequirements: []\n", []);
        var error = Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(new TestPackBuilder().Build(), rules));
        Assert.Equal("No implementation for check type 'attested-field'.", error.Message);
    }
}
