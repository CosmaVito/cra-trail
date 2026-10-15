using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CraTrail.Core.Json;

public static class CraTrailJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public static byte[] Serialize<T>(T value) =>
        WithNewline(JsonSerializer.SerializeToUtf8Bytes(value, Options));

    public static byte[] SerializeNode(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return WithNewline(Encoding.UTF8.GetBytes(node.ToJsonString(Options)));
    }

    public static T Deserialize<T>(byte[] utf8Json) =>
        JsonSerializer.Deserialize<T>(utf8Json, Options)
        ?? throw new JsonException($"Expected a {typeof(T).Name}, found null.");

    private static byte[] WithNewline(byte[] json) => [.. json, (byte)'\n'];
}
