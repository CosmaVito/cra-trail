using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace CraTrail.Core.Json;

public static partial class YamlToJson
{
    public static JsonNode? Convert(string yaml)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException ex)
        {
            throw new FormatException($"Invalid YAML: {ex.Message}", ex);
        }
        if (stream.Documents.Count == 0)
            return null;
        if (stream.Documents.Count > 1)
            throw new FormatException("Expected a single YAML document.");
        return ToJson(stream.Documents[0].RootNode);
    }

    private static JsonNode? ToJson(YamlNode node) => node switch
    {
        YamlMappingNode map => ToObject(map),
        YamlSequenceNode sequence => new JsonArray([.. sequence.Children.Select(ToJson)]),
        YamlScalarNode scalar => ToScalar(scalar),
        _ => throw new FormatException($"Unsupported YAML node at line {node.Start.Line}."),
    };

    private static JsonObject ToObject(YamlMappingNode map)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in map.Children)
        {
            if (key is not YamlScalarNode { Value: { } name })
                throw new FormatException($"Mapping keys must be strings (line {key.Start.Line}).");
            if (obj.ContainsKey(name))
                throw new FormatException($"Duplicate key '{name}' (line {key.Start.Line}).");
            obj[name] = ToJson(value);
        }
        return obj;
    }

    private static JsonNode? ToScalar(YamlScalarNode scalar)
    {
        var text = scalar.Value ?? "";
        if (scalar.Style == ScalarStyle.Plain)
        {
            if (text is "" or "~" or "null")
                return null;
            if (text == "true")
                return JsonValue.Create(true);
            if (text == "false")
                return JsonValue.Create(false);
            if (NumberPattern().IsMatch(text))
                return JsonValue.Create(double.Parse(text, CultureInfo.InvariantCulture));
        }
        return JsonValue.Create(text);
    }

    [GeneratedRegex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?$")]
    private static partial Regex NumberPattern();
}
