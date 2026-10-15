using System.Text.Json.Nodes;
using CraTrail.Core.Evidence;

namespace CraTrail.Tests.Evidence;

public class SarifTests
{
    private static JsonNode Load(string name) => JsonNode.Parse(File.ReadAllBytes(TestPaths.Fixture("sarif", name)))!;

    [Fact]
    public void Rules_in_the_driver_give_level_and_severity()
    {
        Assert.Equal(new[]
        {
            new SarifResult("CodeQL", "js/path-injection", "error", 7.5, "src/routes/docs.ts", 41),
            new SarifResult("CodeQL", "js/missing-rate-limiting", "warning", 5.0, "src/server.ts", 12),
        }, Sarif.ParseResults(Load("codeql-driver.sarif")));
    }

    [Fact]
    public void Rules_in_extensions_are_found_through_the_tool_component() =>
        Assert.Equal(
            new SarifResult("CodeQL", "cs/sql-injection", "error", 8.8, "src/Data/Reports.cs", 57),
            Assert.Single(Sarif.ParseResults(Load("codeql-extensions.sarif"))));

    [Fact]
    public void Results_without_level_or_severity_default_to_warning() =>
        Assert.Equal(
            new SarifResult("betterleaks", "generic-api-key", "warning", null, ".env.example", 1),
            Assert.Single(Sarif.ParseResults(Load("betterleaks.sarif"))));

    [Fact]
    public void The_result_level_wins_over_the_rule_default()
    {
        var log = Load("codeql-driver.sarif");
        log["runs"]![0]!["results"]![0]!["level"] = "note";
        Assert.Equal("note", Sarif.ParseResults(log)[0].Level);
    }

    [Fact]
    public void Redact_removes_snippets_and_fingerprints_from_a_copy()
    {
        var original = Load("betterleaks.sarif");
        var redacted = Sarif.Redact(original).ToJsonString();

        Assert.DoesNotContain("not-a-real-key", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("dev@example.org", redacted, StringComparison.Ordinal);
        Assert.Contains("REDACTED", redacted, StringComparison.Ordinal);
        Assert.Contains("not-a-real-key", original.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_reaches_code_flows_rendered_snippets_and_artifact_contents()
    {
        var log = JsonNode.Parse("""
            { "runs": [ {
                "tool": { "driver": { "name": "x" } },
                "artifacts": [ { "location": { "uri": "a.env" }, "contents": { "text": "token=secret-one" } } ],
                "results": [ {
                  "ruleId": "r1",
                  "locations": [ { "physicalLocation": { "region": { "snippet": { "rendered": { "text": "secret-two" } } } } } ],
                  "codeFlows": [ { "threadFlows": [ { "locations": [ { "location": { "physicalLocation": {
                    "region": { "snippet": { "text": "secret-three" } } } } } ] } ] } ]
                } ]
            } ] }
            """)!;

        var redacted = Sarif.Redact(log).ToJsonString();

        Assert.DoesNotContain("secret-", redacted, StringComparison.Ordinal);
        Assert.Equal("r1", Assert.Single(Sarif.ParseResults(Sarif.Redact(log))).RuleId);
    }

    [Fact]
    public void Other_json_is_rejected() =>
        Assert.Throws<FormatException>(() => Sarif.ParseResults(JsonNode.Parse("""{ "version": "2.1.0" }""")!));
}
