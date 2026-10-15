using System.Text.Json.Nodes;
using CraTrail.Core.Json;

namespace CraTrail.Tests.Json;

public class JsonSelectTests
{
    private static readonly JsonNode Bom = JsonNode.Parse("""
        {
          "metadata": { "timestamp": "2026-10-15T00:00:00Z" },
          "components": [
            {
              "name": "a",
              "hashes": [ { "alg": "SHA-256", "content": "x" }, { "alg": "SHA-512", "content": "y" } ],
              "properties": [ { "name": "bsi:component:filename", "value": "a.dll" } ]
            },
            { "name": "b", "hashes": [] }
          ]
        }
        """)!;

    [Fact]
    public void Follows_plain_property_names() =>
        Assert.Equal("2026-10-15T00:00:00Z", Texts("metadata.timestamp").Single());

    [Fact]
    public void Wildcard_selects_every_array_element() =>
        Assert.Equal(new[] { "a", "b" }, Texts("components[*].name"));

    [Fact]
    public void Filter_keeps_elements_whose_property_matches() =>
        Assert.Equal(new[] { "y" }, Texts("components[*].hashes[alg=SHA-512].content"));

    [Fact]
    public void Filter_value_may_contain_colons() =>
        Assert.Equal(new[] { "a.dll" }, Texts("components[*].properties[name=bsi:component:filename].value"));

    [Fact]
    public void Filter_applies_to_a_single_object() =>
        Assert.Single(JsonSelect.Select(
            JsonNode.Parse("""{ "license": { "id": "MIT", "acknowledgement": "concluded" } }"""),
            "license[acknowledgement=concluded]"));

    [Fact]
    public void Missing_properties_and_nulls_select_nothing()
    {
        Assert.Empty(JsonSelect.Select(Bom, "metadata.manufacturer.name"));
        Assert.Empty(JsonSelect.Select(JsonNode.Parse("""{ "a": null }"""), "a"));
        Assert.Empty(JsonSelect.Select(null, "a"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a.")]
    [InlineData("a..b")]
    [InlineData("a[*")]
    [InlineData("a[=x]")]
    [InlineData("a]b")]
    public void Invalid_paths_are_rejected(string path) => Assert.False(JsonSelect.IsValid(path));

    private static List<string> Texts(string path) =>
        [.. JsonSelect.Select(Bom, path).Select(n => n.GetValue<string>())];
}
