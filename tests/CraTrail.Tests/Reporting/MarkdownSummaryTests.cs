using System.Text.Json.Nodes;
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
            Render(Result(new GateOutcome(false, ["no-kev-without-vex", "sbom-core-fields"], [KevBlock]))));

    [Fact]
    public void A_passed_gate_has_no_blocking_section()
    {
        var text = Render(Result(new GateOutcome(true, ["no-kev-without-vex"], [])));
        Assert.StartsWith(
            "## cra-trail gate: passed\n\nCVD Inbox 1.2.0 · rules 2026.10\n\nGate checks: `no-kev-without-vex`.\n\n### Annex I\n",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Blocking", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_detail_lists_are_cut_at_twenty()
    {
        var block = KevBlock with { Details = [.. Enumerable.Range(1, 25).Select(i => $"finding {i:00}")] };
        var text = Render(Result(new GateOutcome(false, [block.Check], [block])));
        Assert.Contains("- `finding 20`\n- and 5 more\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("finding 21", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Pipes_in_titles_are_escaped()
    {
        var result = Result(new GateOutcome(true, [], [])) with
        {
            Requirements = [new RequirementOutcome("x", "X", "A | B", "manual", Statuses.Missing, [], null, null, [])],
        };
        Assert.Contains("| X | A \\| B | manual | missing |", Render(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_text_cannot_add_lines_links_or_html()
    {
        var block = KevBlock with
        {
            Details = ["GHSA-1 pkg:npm/evil@1.0.0\n## Gate passed\n[click](https://evil.example)", "uses `back`ticks", "<img src=x onerror=alert(1)>"],
        };
        var manifest = Manifest with { Product = Manifest.Product with { Name = "Evil <b>product</b>\n# Title" } };

        var text = MarkdownSummary.Render(Result(new GateOutcome(false, [block.Check], [block])), manifest, TestProducts.Default);

        Assert.DoesNotContain("\n## Gate passed", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n# Title", text, StringComparison.Ordinal);
        Assert.Contains("- `GHSA-1 pkg:npm/evil@1.0.0 ## Gate passed [click](https://evil.example)`\n", text, StringComparison.Ordinal);
        Assert.Contains("- ``uses `back`ticks``\n", text, StringComparison.Ordinal);
        Assert.Contains("- `<img src=x onerror=alert(1)>`\n", text, StringComparison.Ordinal);
        Assert.StartsWith("## cra-trail gate: blocked\n\nEvil \\<b\\>product\\</b\\> \\# Title 1.2.0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_summary_shows_where_cra_yml_moved_the_gate_away_from_the_rules()
    {
        var product = TestProducts.Default with
        {
            GateExclude = ["no-critical-without-vex"],
            GateInclude = ["sbom-bsi-fields"],
            CheckOverrides = TestProducts.Map(("no-kev-without-vex", new JsonObject { ["select"] = new JsonObject() })),
        };

        var text = MarkdownSummary.Render(Result(new GateOutcome(true, ["no-kev-without-vex", "sbom-bsi-fields"], [])), Manifest, product);

        Assert.Contains(
            "Gate checks: `no-kev-without-vex`, `sbom-bsi-fields`.\n\n" +
            "- Left out of the gate by cra.yml: `no-critical-without-vex`\n" +
            "- Added to the gate by cra.yml: `sbom-bsi-fields`\n" +
            "- Settings changed by cra.yml: `no-kev-without-vex`\n\n",
            text,
            StringComparison.Ordinal);
    }

    private static string Render(EvaluationResult result) => MarkdownSummary.Render(result, Manifest, TestProducts.Default);

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
