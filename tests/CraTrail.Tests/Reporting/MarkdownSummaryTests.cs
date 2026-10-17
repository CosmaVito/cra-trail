using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Pack;
using CraTrail.Core.Reporting;

namespace CraTrail.Tests.Reporting;

public partial class MarkdownSummaryTests
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
            "## cra-trail gate: passed\n\n`CVD Inbox 1.2.0` · rules `2026.10`\n\nGate checks: `no-kev-without-vex`.\n\n### Annex I\n",
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
            Requirements =
            [
                new RequirementOutcome("x", "X", "A | B", "manual", Statuses.Missing, [], null, null, []),
                // A backslash before the pipe would pair with the escape and end the cell.
                new RequirementOutcome("y", "Y", "C \\| D", "manual", Statuses.Missing, [], null, null, []),
            ],
        };
        var text = Render(result);
        Assert.Contains("| `X` | `A \\| B` | `manual` | `missing` |", text, StringComparison.Ordinal);
        Assert.Contains("| `Y` | `C \\| D` | `manual` | `missing` |", text, StringComparison.Ordinal);
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
        Assert.StartsWith("## cra-trail gate: blocked\n\n`Evil <b>product</b> # Title 1.2.0`", text, StringComparison.Ordinal);
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

    [Fact]
    public void Evidence_text_stays_in_code_spans_where_it_cannot_become_a_link_a_mention_or_math()
    {
        // GitHub links bare URLs, www. hosts, e-mail addresses and @mentions, and renders $...$ as math, escaped or not.
        const string Evil = "https://evil.example www.evil.example a@evil.example @octocat $x^2$";
        var block = KevBlock with { Status = Evil, Hint = Evil };
        var result = Result(new GateOutcome(false, [block.Check], [block])) with
        {
            RulesVersion = Evil,
            Requirements = [new RequirementOutcome(Evil, Evil, Evil, Evil, Evil, [], null, null, [])],
        };
        var manifest = Manifest with { Product = Manifest.Product with { Name = Evil, Version = Evil } };

        var outside = CodeSpan().Replace(MarkdownSummary.Render(result, manifest, TestProducts.Default), "");

        Assert.DoesNotContain("evil", outside, StringComparison.Ordinal);
        Assert.DoesNotContain("@", outside, StringComparison.Ordinal);
        Assert.DoesNotContain("$", outside, StringComparison.Ordinal);
    }

    private static string Render(EvaluationResult result) => MarkdownSummary.Render(result, Manifest, TestProducts.Default);

    [GeneratedRegex(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)")]
    private static partial Regex CodeSpan();

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
