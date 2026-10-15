using System.Text.Json.Nodes;

namespace CraTrail.Core.Json;

public static class ComponentWalker
{
    public static IReadOnlyList<JsonObject> Components(JsonNode? bom)
    {
        var result = new List<JsonObject>();
        Walk(JsonAccess.Child(bom, "components"), result);
        return result;
    }

    public static IReadOnlyList<JsonObject> AllComponents(JsonNode? bom)
    {
        var result = new List<JsonObject>();
        if (JsonAccess.Child(JsonAccess.Child(bom, "metadata"), "component") is JsonObject primary)
        {
            result.Add(primary);
            Walk(JsonAccess.Child(primary, "components"), result);
        }
        Walk(JsonAccess.Child(bom, "components"), result);
        return result;
    }

    public static bool IsAssembly(JsonObject component) =>
        JsonAccess.Child(component, "components") is JsonArray { Count: > 0 };

    public static string Reference(JsonObject component, int index) =>
        NonBlank(JsonAccess.Child(component, "bom-ref"))
        ?? NonBlank(JsonAccess.Child(component, "purl"))
        ?? NonBlank(JsonAccess.Child(component, "name"))
        ?? $"#{index}";

    private static void Walk(JsonNode? components, List<JsonObject> result)
    {
        foreach (var item in JsonAccess.Items(components))
        {
            if (item is not JsonObject component)
                continue;
            result.Add(component);
            Walk(JsonAccess.Child(component, "components"), result);
        }
    }

    private static string? NonBlank(JsonNode? node) =>
        JsonAccess.Text(node) is { } text && !string.IsNullOrWhiteSpace(text) ? text : null;
}
