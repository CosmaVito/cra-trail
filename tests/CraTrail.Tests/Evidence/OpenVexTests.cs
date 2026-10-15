using System.Text.Json.Nodes;
using CraTrail.Core.Evidence;

namespace CraTrail.Tests.Evidence;

public class OpenVexTests
{
    private static readonly string[] KevIds = ["GHSA-jfh8-c2jp-5v3q", "CVE-2021-44228"];
    private const string Log4j = "pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1";

    [Fact]
    public void A_statement_matches_through_an_alias_of_the_finding()
    {
        var index = OpenVex.BuildIndex([Document("""
            [ { "vulnerability": { "name": "CVE-2021-44228" },
                "products": [ { "@id": "pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1?type=jar" } ],
                "status": "not_affected", "justification": "vulnerable_code_not_in_execute_path" } ]
            """)]);
        Assert.Equal("not_affected", index.Resolve(KevIds, Log4j)?.Status);
    }

    [Fact]
    public void A_statement_matches_through_its_own_aliases()
    {
        var index = OpenVex.BuildIndex([Document("""
            [ { "vulnerability": { "name": "CVE-2024-21907", "aliases": ["GHSA-5crp-9r3c-p9vr"] },
                "products": [ { "identifiers": { "purl": "pkg:nuget/newtonsoft.json@12.0.1" } } ],
                "status": "fixed" } ]
            """)]);
        Assert.Equal("fixed", index.Resolve(["GHSA-5crp-9r3c-p9vr"], "pkg:nuget/Newtonsoft.Json@12.0.1")?.Status);
    }

    [Fact]
    public void Statements_for_other_products_do_not_match()
    {
        var index = OpenVex.BuildIndex([Document("""
            [ { "vulnerability": { "name": "CVE-2021-44228" }, "products": [ { "@id": "pkg:npm/other@1.0.0" } ], "status": "fixed" } ]
            """)]);
        Assert.Null(index.Resolve(KevIds, Log4j));
    }

    [Fact]
    public void A_statement_without_products_applies_to_every_product()
    {
        var index = OpenVex.BuildIndex([Document("""[ { "vulnerability": { "name": "CVE-2021-44228" }, "status": "fixed" } ]""")]);
        Assert.True(index.Statements[0].AllProducts);
        Assert.Equal("fixed", index.Resolve(KevIds, Log4j)?.Status);
    }

    [Fact]
    public void A_product_that_is_not_a_purl_matches_nothing()
    {
        var index = OpenVex.BuildIndex([Document("""
            [ { "vulnerability": { "name": "CVE-2021-44228" }, "products": [ { "@id": "https://example.org/product" } ], "status": "fixed" } ]
            """)]);
        Assert.False(index.Statements[0].AllProducts);
        Assert.Null(index.Resolve(KevIds, Log4j));
    }

    [Fact]
    public void The_latest_statement_wins()
    {
        var index = OpenVex.BuildIndex([Document("""
            [
              { "vulnerability": { "name": "CVE-2021-44228" }, "status": "affected", "action_statement": "Upgrade.", "timestamp": "2026-10-05T00:00:00Z" },
              { "vulnerability": { "name": "CVE-2021-44228" }, "status": "not_affected", "justification": "component_not_present", "timestamp": "2026-10-01T00:00:00Z" }
            ]
            """)]);
        Assert.Equal("affected", index.Resolve(KevIds, Log4j)?.Status);
    }

    [Fact]
    public void Document_timestamps_apply_and_reading_order_breaks_ties()
    {
        var index = OpenVex.BuildIndex([
            Document("""[ { "vulnerability": { "name": "CVE-2021-44228" }, "status": "not_affected", "justification": "component_not_present" } ]"""),
            Document("""[ { "vulnerability": { "name": "CVE-2021-44228" }, "status": "affected", "action_statement": "Upgrade." } ]"""),
        ]);
        Assert.Equal(new[] { 0, 1 }, index.Statements.Select(s => s.Order));
        Assert.Equal("affected", index.Resolve(KevIds, Log4j)?.Status);
    }

    [Theory]
    [InlineData("""{ "statements": [] }""")]
    [InlineData("""{ "@context": "https://openvex.dev/ns/v0.2.0", "statements": [ { "status": "fixed" } ] }""")]
    [InlineData("""{ "@context": "https://openvex.dev/ns/v0.2.0", "statements": [ { "vulnerability": { "name": "X" }, "status": "maybe" } ] }""")]
    public void Invalid_documents_are_rejected(string json) =>
        Assert.Throws<FormatException>(() => OpenVex.ParseStatements(JsonNode.Parse(json)!));

    [Fact]
    public void Merge_keeps_every_statement_with_its_timestamp()
    {
        var merged = OpenVex.Merge(
            [
                Document("""[ { "vulnerability": { "name": "A" }, "status": "fixed" } ]"""),
                Document("""[ { "vulnerability": { "name": "B" }, "status": "fixed" } ]""", "2026-10-09T00:00:00Z"),
            ],
            "urn:cra-trail:vex:x:1", "cra-trail", new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(OpenVex.Context, merged["@context"]!.GetValue<string>());
        Assert.Equal("urn:cra-trail:vex:x:1", merged["@id"]!.GetValue<string>());
        Assert.Equal("2026-10-15T00:00:00Z", merged["timestamp"]!.GetValue<string>());
        var statements = merged["statements"]!.AsArray();
        Assert.Equal(2, statements.Count);
        Assert.Equal("2026-10-01T00:00:00Z", statements[0]!["timestamp"]!.GetValue<string>());
        Assert.Equal("2026-10-09T00:00:00Z", statements[1]!["timestamp"]!.GetValue<string>());
        Assert.Equal(2, OpenVex.ParseStatements(merged).Count);
    }

    private static JsonNode Document(string statements, string timestamp = "2026-10-01T00:00:00Z") => JsonNode.Parse($$"""
        {
          "@context": "https://openvex.dev/ns/v0.2.0",
          "@id": "https://example.org/vex/1",
          "author": "Example",
          "timestamp": "{{timestamp}}",
          "version": 1,
          "statements": {{statements}}
        }
        """)!;
}
