using System.Text.Json.Nodes;
using CraTrail.Core.Json;

namespace CraTrail.Tests.Json;

public class ComponentWalkerTests
{
    private static readonly JsonNode Bom = JsonNode.Parse("""
        {
          "metadata": { "component": { "bom-ref": "app", "name": "app", "components": [ { "name": "plugin" } ] } },
          "components": [
            { "bom-ref": "backend", "name": "backend", "components": [ { "purl": "pkg:nuget/A@1.0.0", "name": "A" }, { "name": "B" } ] },
            { }
          ]
        }
        """)!;

    [Fact]
    public void Components_walks_the_tree_depth_first() =>
        Assert.Equal(new[] { "backend", "pkg:nuget/A@1.0.0", "B", "#3" }, References(ComponentWalker.Components(Bom)));

    [Fact]
    public void All_components_starts_with_the_primary_component() =>
        Assert.Equal(
            new[] { "app", "plugin", "backend", "pkg:nuget/A@1.0.0", "B", "#5" },
            References(ComponentWalker.AllComponents(Bom)));

    [Fact]
    public void An_assembly_is_a_component_with_children()
    {
        var components = ComponentWalker.Components(Bom);
        Assert.True(ComponentWalker.IsAssembly(components[0]));
        Assert.False(ComponentWalker.IsAssembly(components[1]));
    }

    [Fact]
    public void A_document_that_is_not_an_object_has_no_components() =>
        Assert.Empty(ComponentWalker.AllComponents(JsonNode.Parse("[1, 2]")));

    private static string[] References(IReadOnlyList<JsonObject> components) =>
        [.. components.Select((c, i) => ComponentWalker.Reference(c, i))];
}
