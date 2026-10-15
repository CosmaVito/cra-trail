using CraTrail.Core.Evaluation;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Evaluation;

public class GateTests
{
    private static RuleSet Rules => TestRules.Conformance;

    [Fact]
    public void Passes_when_every_required_check_passes()
    {
        var gate = Gate.Decide(Rules, TestProducts.Default, Outcomes());

        Assert.True(gate.Passed);
        Assert.Empty(gate.Blocking);
        Assert.Equal(new[] { "no-kev-without-vex", "no-critical-without-vex", "no-secrets", "sbom-core-fields", "support-end-set" }, gate.Required);
    }

    [Fact]
    public void Fail_and_missing_block_with_details_and_hint()
    {
        var gate = Gate.Decide(Rules, TestProducts.Default, Outcomes(
            ("no-secrets", Statuses.Missing, new[] { "no file of kind secrets-sarif" }),
            ("no-kev-without-vex", Statuses.Fail, new[] { "GHSA-x pkg:npm/x@1" }),
            ("sast-no-high", Statuses.Fail, new[] { "not required" })));

        Assert.False(gate.Passed);
        Assert.Equal(new[] { "no-kev-without-vex", "no-secrets" }, gate.Blocking.Select(b => b.Check));
        var kev = gate.Blocking[0];
        Assert.Equal(Statuses.Fail, kev.Status);
        Assert.Equal(new[] { "GHSA-x pkg:npm/x@1" }, kev.Details);
        Assert.Equal("Upgrade the component, or add an OpenVEX statement with status not_affected or fixed.", kev.Hint);
        Assert.Equal(Statuses.Missing, gate.Blocking[1].Status);
    }

    [Fact]
    public void Exclude_removes_and_include_appends_in_order()
    {
        var product = TestProducts.Default with { GateExclude = ["no-kev-without-vex"], GateInclude = ["sbom-bsi-fields"] };
        var gate = Gate.Decide(Rules, product, Outcomes(
            ("no-kev-without-vex", Statuses.Fail, new[] { "ignored" }),
            ("sbom-bsi-fields", Statuses.Fail, new[] { "sbom-creator" })));

        Assert.Equal(
            new[] { "no-critical-without-vex", "no-secrets", "sbom-core-fields", "support-end-set", "sbom-bsi-fields" },
            gate.Required);
        var block = Assert.Single(gate.Blocking);
        Assert.Equal("sbom-bsi-fields", block.Check);
        Assert.Equal("Add the BSI TR-03183-2 fields to the SBOM, or set gate.require-bsi to false.", block.Hint);
    }

    [Fact]
    public void An_unknown_required_check_blocks_as_missing()
    {
        var gate = Gate.Decide(Rules, TestProducts.Default with { GateInclude = ["ghost"] }, Outcomes());

        var block = Assert.Single(gate.Blocking);
        Assert.Equal("ghost", block.Check);
        Assert.Equal(Statuses.Missing, block.Status);
        Assert.Equal(new[] { "unknown check" }, block.Details);
        Assert.Null(block.Hint);
    }

    private static List<CheckOutcome> Outcomes(params (string Id, string Status, string[] Details)[] changes)
    {
        var outcomes = Rules.Checks.Select(c => new CheckOutcome(c.Id, c.Type, Statuses.Pass, [])).ToList();
        foreach (var (id, status, details) in changes)
        {
            var index = outcomes.FindIndex(o => o.Id == id);
            outcomes[index] = outcomes[index] with { Status = status, Details = details };
        }
        return outcomes;
    }
}
