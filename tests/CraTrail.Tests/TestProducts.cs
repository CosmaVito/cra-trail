using System.Text.Json.Nodes;
using CraTrail.Core.Product;

namespace CraTrail.Tests;

internal static class TestProducts
{
    public static ProductInfo Default { get; } = new(
        "CVD Inbox",
        "1.2.0",
        "pkg:github/CosmaVito/cra-trail#demo/cvd-inbox",
        "2031-12-31",
        "security@example.org",
        "https://example.org/.well-known/security.txt",
        Map<string>(),
        Map<string>(),
        Map<string>(),
        Map<JsonObject>(),
        [],
        []);

    public static SortedDictionary<string, T> Map<T>(params (string Key, T Value)[] entries)
    {
        var map = new SortedDictionary<string, T>(StringComparer.Ordinal);
        foreach (var (key, value) in entries)
            map[key] = value;
        return map;
    }
}
