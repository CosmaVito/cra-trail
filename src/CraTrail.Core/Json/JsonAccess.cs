using System.Text.Json.Nodes;

namespace CraTrail.Core.Json;

internal static class JsonAccess
{
    public static JsonNode? Child(JsonNode? node, string name) =>
        node is JsonObject obj && obj.TryGetPropertyValue(name, out var child) ? child : null;

    public static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    public static double? Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out double number) ? number : null;

    public static bool? Bool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out bool flag) ? flag : null;

    public static IEnumerable<JsonNode> Items(JsonNode? node) =>
        node is JsonArray array ? array.OfType<JsonNode>() : [];
}
