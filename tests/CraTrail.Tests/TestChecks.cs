using System.Text.Json.Nodes;
using CraTrail.Core.Json;
using CraTrail.Core.Rules;

namespace CraTrail.Tests;

internal static class TestChecks
{
    public static CheckDefinition Define(string id, string yaml)
    {
        var body = (JsonObject)YamlToJson.Convert(yaml)!;
        var type = body["type"]!.GetValue<string>();
        body.Remove("type");
        return new CheckDefinition(id, type, body, null);
    }
}
