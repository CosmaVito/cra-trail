using CraTrail.Core.Evaluation;
using CraTrail.Core.Evaluation.Checks;
using CraTrail.Core.Pack;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Evaluation.Checks;

public class VexCoversCheckTests
{
    private static readonly CheckDefinition NoKev = TestChecks.Define("no-kev-without-vex", "type: vex-covers\nkind: grype\nselect:\n  kev: true");
    private static readonly CheckDefinition NoCritical = TestChecks.Define("no-critical-without-vex", "type: vex-covers\nkind: grype\nselect:\n  min-cvss: 9.0");
    private static readonly CheckDefinition Every = TestChecks.Define("every", "type: vex-covers\nkind: grype");
    private const string Log4j = "GHSA-jfh8-c2jp-5v3q pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1";

    [Fact]
    public void Is_missing_without_a_grype_report()
    {
        var outcome = Evaluate(NoKev, new TestPackBuilder());
        Assert.Equal(Statuses.Missing, outcome.Status);
        Assert.Equal(new[] { "no file of kind grype" }, outcome.Details);
    }

    [Fact]
    public void Fails_for_a_kev_finding_without_vex()
    {
        var outcome = Evaluate(NoKev, Report("grype-kev.json"));
        Assert.Equal(Statuses.Fail, outcome.Status);
        Assert.Equal(new[] { Log4j }, outcome.Details);
    }

    [Fact]
    public void Passes_when_a_statement_names_an_alias_of_the_finding() =>
        Assert.Equal(Statuses.Pass, Evaluate(NoKev, Report("grype-kev.json").AddShared("vex/product.openvex.json", EvidenceKinds.Vex, "vex-kev-alias.openvex.json")).Status);

    [Theory]
    [InlineData("affected", "\"action_statement\": \"Upgrade.\"", "fail")]
    [InlineData("under_investigation", "\"status_notes\": \"Checking.\"", "fail")]
    [InlineData("fixed", "\"status_notes\": \"Patched.\"", "pass")]
    [InlineData("not_affected", "\"justification\": \"component_not_present\"", "pass")]
    public void Only_not_affected_and_fixed_cover_a_finding(string status, string extra, string expected)
    {
        var vex = Vex($$"""[ { "vulnerability": { "name": "CVE-2021-44228" }, "status": "{{status}}", {{extra}} } ]""");
        Assert.Equal(expected, Evaluate(NoKev, Report("grype-kev.json").Add("vex/product.openvex.json", EvidenceKinds.Vex, vex)).Status);
    }

    [Fact]
    public void The_latest_statement_wins()
    {
        var vex = Vex("""
            [
              { "vulnerability": { "name": "CVE-2021-44228" }, "status": "not_affected", "justification": "component_not_present", "timestamp": "2026-10-01T00:00:00Z" },
              { "vulnerability": { "name": "CVE-2021-44228" }, "status": "affected", "action_statement": "Upgrade.", "timestamp": "2026-10-05T00:00:00Z" }
            ]
            """);
        Assert.Equal(Statuses.Fail, Evaluate(NoKev, Report("grype-kev.json").Add("vex/product.openvex.json", EvidenceKinds.Vex, vex)).Status);
    }

    [Fact]
    public void Min_cvss_uses_the_severity_when_cvss_is_missing()
    {
        var outcome = Evaluate(NoCritical, Report("grype-critical-no-cvss.json"));
        Assert.Equal(Statuses.Fail, outcome.Status);
        Assert.Equal(new[] { "GHSA-aaaa-bbbb-cccc pkg:npm/example-lib@1.0.0" }, outcome.Details);
    }

    [Fact]
    public void Findings_below_the_threshold_or_outside_kev_are_not_selected()
    {
        Assert.Equal(Statuses.Pass, Evaluate(NoCritical, Report("grype-high.json")).Status);
        Assert.Equal(Statuses.Pass, Evaluate(NoKev, Report("grype-high.json")).Status);
    }

    [Fact]
    public void Without_a_selector_every_finding_counts() =>
        Assert.Equal(new[] { "GHSA-5crp-9r3c-p9vr pkg:nuget/Newtonsoft.Json@12.0.1" }, Evaluate(Every, Report("grype-high.json")).Details);

    [Fact]
    public void A_finding_without_purl_is_named_by_component_and_version()
    {
        var grype = File.ReadAllText(TestPaths.Conformance("shared", "grype-high.json"))
            .Replace("\"purl\": \"pkg:nuget/Newtonsoft.Json@12.0.1\"", "\"purl\": \"\"", StringComparison.Ordinal);
        var outcome = Evaluate(Every, new TestPackBuilder().Add("scans/grype.json", EvidenceKinds.Grype, grype));
        Assert.Equal(new[] { "GHSA-5crp-9r3c-p9vr Newtonsoft.Json@12.0.1" }, outcome.Details);
    }

    [Fact]
    public void Broken_evidence_fails_with_the_file_name()
    {
        var outcome = Evaluate(NoKev, new TestPackBuilder().Add("scans/grype.json", EvidenceKinds.Grype, "{}"));
        Assert.Equal(Statuses.Fail, outcome.Status);
        Assert.Equal(new[] { "invalid evidence: scans/grype.json" }, outcome.Details);
    }

    private static TestPackBuilder Report(string name) =>
        new TestPackBuilder().AddShared("scans/grype.json", EvidenceKinds.Grype, name);

    private static string Vex(string statements) => $$"""
        { "@context": "https://openvex.dev/ns/v0.2.0", "@id": "https://example.org/vex/1", "author": "Example",
          "timestamp": "2026-10-01T00:00:00Z", "version": 1, "statements": {{statements}} }
        """;

    private static CheckOutcome Evaluate(CheckDefinition check, TestPackBuilder pack) =>
        new VexCoversCheck().Evaluate(check, pack.Context());
}
