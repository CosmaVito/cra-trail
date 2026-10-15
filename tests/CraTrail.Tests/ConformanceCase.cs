using System.Text.Json.Nodes;
using CraTrail.Core.Json;
using CraTrail.Core.Pack;
using CraTrail.Core.Product;

namespace CraTrail.Tests;

internal static class ConformanceCase
{
    public static PackContents LoadPack(string caseDirectory, string rulesVersion)
    {
        var spec = JsonNode.Parse(File.ReadAllBytes(Path.Combine(caseDirectory, "pack.json")))!;
        var root = Path.GetFullPath(TestPaths.Conformance());
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var entries = new List<PackFile>();
        foreach (var item in spec["files"]!.AsArray())
        {
            var path = item!["path"]!.GetValue<string>();
            var source = Path.GetFullPath(Path.Combine(caseDirectory, item["source"]!.GetValue<string>()));
            if (!source.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{source} is outside the conformance folder.");
            var bytes = File.ReadAllBytes(source);
            files[path] = bytes;
            entries.Add(new PackFile(path, item["kind"]!.GetValue<string>(), MediaTypes.For(path), bytes.LongLength, PackHashing.Sha256Hex(bytes)));
        }

        var product = CraTrailJson.Deserialize<ProductInfo>(files[PackContents.ProductPath]);
        var signing = spec["signing"] is JsonObject s
            ? new SigningInfo(s["method"]!.GetValue<string>(), s["signerWorkflow"]?.GetValue<string>())
            : null;
        var manifest = new PackManifest(
            PackManifest.CurrentSchemaVersion,
            new GeneratorInfo("cra-trail", "conformance"),
            rulesVersion,
            new ProductRef(product.Name, product.Version, product.Purl),
            null,
            signing,
            spec["createdAt"]!.GetValue<string>(),
            [],
            [.. entries.OrderBy(e => e.Path, StringComparer.Ordinal)]);
        return PackContents.Create(manifest, files);
    }
}
