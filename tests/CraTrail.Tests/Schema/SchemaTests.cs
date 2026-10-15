using System.Text.Json;
using System.Text.Json.Nodes;
using CraTrail.Core.Json;
using Json.Schema;

namespace CraTrail.Tests.Schema;

public class SchemaTests
{
    public static TheoryData<string> Expected()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(TestPaths.Conformance("cases"), "expected.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            data.Add(Path.GetRelativePath(TestPaths.Conformance(), file));
        return data;
    }

    [Fact]
    public void A_manifest_matches_its_schema() =>
        AssertValid("manifest", new TestPackBuilder().WithSigning("github-artifact-attestations").Build().ManifestBytes);

    [Fact]
    public void Product_json_matches_its_schema() =>
        AssertValid("product", CraTrailJson.Serialize(TestProducts.Default));

    [Theory]
    [MemberData(nameof(Expected))]
    public void Every_expected_evaluation_matches_its_schema(string relative) =>
        AssertValid("evaluation", File.ReadAllBytes(Path.Combine(TestPaths.Conformance(), relative)));

    [Fact]
    public void An_uppercase_hash_is_rejected()
    {
        var manifest = JsonNode.Parse(new TestPackBuilder().Build().ManifestBytes)!;
        var sha = manifest["files"]![0]!["sha256"]!.GetValue<string>();
        manifest["files"]![0]!["sha256"] = sha.ToUpperInvariant();
        Assert.False(Evaluate("manifest", CraTrailJson.SerializeNode(manifest)).IsValid);
    }

    private static void AssertValid(string schema, byte[] json)
    {
        var result = Evaluate(schema, json);
        Assert.True(result.IsValid, JsonSerializer.Serialize(result));
    }

    private static EvaluationResults Evaluate(string schema, byte[] json)
    {
        var compiled = JsonSchema.FromText(
            File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "schema", schema + ".schema.json")),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        using var document = JsonDocument.Parse(json);
        return compiled.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
    }
}
