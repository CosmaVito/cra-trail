using CraTrail.Core.Evaluation;
using CraTrail.Core.Pack;
using CraTrail.Core.Reporting;

namespace CraTrail.Tests.Reporting;

public class MarkdownSummaryTests
{
    private static readonly PackManifest Manifest = new TestPackBuilder().Build().Manifest;

    private static readonly GateBlock KevBlock = new(
        "no-kev-without-vex",
        Statuses.Fail,
        ["GHSA-jfh8-c2jp-5v3q pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1"],
        "Upgrade the component, or add an OpenVEX statement with status not_affected or fixed.");

    [Fact]
    public void A_blocked_gate_lists_blocks_hints_and_the_annex_table() =>
        Assert.Equal(
            File.ReadAllText(TestPaths.Fixture("summary", "blocked.md")),
            MarkdownSummary.Render(Result(new GateOutcome(false, ["no-kev-without-vex", "sbom-core-fields"], [KevBlock])), Manifest));

    [Fact]
    public void A_passed_gate_has_no_blocking_section()
    {
        var text = MarkdownSummary.Render(Result(new GateOutcome(true, ["no-kev-without-vex"], [])), Manifest);
        Assert.StartsWith("## cra-trail gate: passed\n\nCVD Inbox 1.2.0 · rules 2026.10\n\n### Annex I\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Blocking", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_detail_lists_are_cut_at_twenty()
    {
        var block = KevBlock with { Details = [.. Enumerable.Range(1, 25).Select(i => $"finding {i:00}")] };
        var text = MarkdownSummary.Render(Result(new GateOutcome(false, [block.Check], [block])), Manifest);
        Assert.Contains("- finding 20\n- and 5 more\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("finding 21", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Pipes_in_titles_are_escaped()
    {
        var result = Result(new GateOutcome(true, [], [])) with
        {
            Requirements = [new RequirementOutcome("x", "X", "A | B", "manual", Statuses.Missing, [], null, null, [])],
        };
        Assert.Contains("| X | A \\| B | manual | missing |", MarkdownSummary.Render(result, Manifest), StringComparison.Ordinal);
    }

    private static EvaluationResult Result(GateOutcome gate) => new(
        "0.1",
        "2026.10",
        [],
        [
            new RequirementOutcome("annex-i/part-i/1", "I (1)", "Appropriate level of cybersecurity based on the risks", "manual", Statuses.Attested, [], "attested/docs/risk.md", null, []),
            new RequirementOutcome("annex-i/part-i/2a", "I (2)(a)", "No known exploitable vulnerabilities", "automated", Statuses.Fail, ["no-kev-without-vex"], null, null, []),
        ],
        gate,
        new StatusTotals(0, 1, 1, 0, 0));
}
