using CraTrail.Core.Evaluation;
using CraTrail.Core.Evaluation.Checks;
using CraTrail.Core.Pack;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Evaluation.Checks;

public class ScanThresholdCheckTests
{
    private static readonly CheckDefinition NoSecrets = TestChecks.Define("no-secrets", "type: scan-threshold\nkind: secrets-sarif\nmax-results: 0");

    [Fact]
    public void Is_missing_without_a_file_of_the_kind()
    {
        var outcome = Evaluate(NoSecrets, new TestPackBuilder());
        Assert.Equal(Statuses.Missing, outcome.Status);
        Assert.Equal(new[] { "no file of kind secrets-sarif" }, outcome.Details);
    }

    [Fact]
    public void Passes_when_no_result_is_counted() =>
        Assert.Equal(Statuses.Pass, Evaluate(NoSecrets, new TestPackBuilder().AddShared("scans/secrets.sarif", EvidenceKinds.SecretsSarif, "secrets-clean.sarif")).Status);

    [Fact]
    public void Fails_and_lists_each_counted_result()
    {
        var outcome = Evaluate(NoSecrets, Betterleaks());
        Assert.Equal(Statuses.Fail, outcome.Status);
        Assert.Equal(new[] { "generic-api-key .env.example:1" }, outcome.Details);
    }

    [Fact]
    public void Allows_up_to_max_results() =>
        Assert.Equal(Statuses.Pass, Evaluate(TestChecks.Define("c", "type: scan-threshold\nkind: secrets-sarif\nmax-results: 1"), Betterleaks()).Status);

    [Theory]
    [InlineData("min-security-severity: 7.0", "js/path-injection src/routes/docs.ts:41")]
    [InlineData("min-security-severity: 4.0", "js/missing-rate-limiting src/server.ts:12|js/path-injection src/routes/docs.ts:41")]
    [InlineData("levels: [error]", "js/path-injection src/routes/docs.ts:41")]
    public void Filters_by_security_severity_and_level(string filter, string expected)
    {
        var outcome = Evaluate(Sast(filter), CodeQl());
        Assert.Equal(Statuses.Fail, outcome.Status);
        Assert.Equal(expected.Split('|'), outcome.Details);
    }

    [Fact]
    public void Nothing_counted_after_filtering_passes() =>
        Assert.Equal(Statuses.Pass, Evaluate(Sast("levels: [warning]\nmin-security-severity: 7.0"), CodeQl()).Status);

    [Fact]
    public void A_result_without_location_is_written_with_a_dash()
    {
        var sarif = """{ "runs": [ { "tool": { "driver": { "name": "x" } }, "results": [ { "ruleId": "r1" } ] } ] }""";
        var outcome = Evaluate(NoSecrets, new TestPackBuilder().Add("scans/secrets.sarif", EvidenceKinds.SecretsSarif, sarif));
        Assert.Equal(new[] { "r1 -" }, outcome.Details);
    }

    [Fact]
    public void Broken_evidence_fails_with_the_file_name()
    {
        var outcome = Evaluate(NoSecrets, new TestPackBuilder().Add("scans/secrets.sarif", EvidenceKinds.SecretsSarif, "not json"));
        Assert.Equal(Statuses.Fail, outcome.Status);
        Assert.Equal(new[] { "invalid evidence: scans/secrets.sarif" }, outcome.Details);
    }

    private static CheckDefinition Sast(string filter) =>
        TestChecks.Define("sast", $"type: scan-threshold\nkind: sast-sarif\n{filter}");

    private static TestPackBuilder Betterleaks() =>
        new TestPackBuilder().AddFixture("scans/secrets.sarif", EvidenceKinds.SecretsSarif, "sarif", "betterleaks.sarif");

    private static TestPackBuilder CodeQl() =>
        new TestPackBuilder().AddFixture("scans/codeql.sarif", EvidenceKinds.SastSarif, "sarif", "codeql-driver.sarif");

    private static CheckOutcome Evaluate(CheckDefinition check, TestPackBuilder pack) =>
        new ScanThresholdCheck().Evaluate(check, pack.Context());
}
