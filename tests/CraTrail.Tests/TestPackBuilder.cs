using System.Text;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Json;
using CraTrail.Core.Pack;
using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Tests;

internal sealed class TestPackBuilder
{
    private readonly SortedDictionary<string, (string Kind, byte[] Bytes)> _files = new(StringComparer.Ordinal);
    private ProductInfo _product = TestProducts.Default;
    private SigningInfo? _signing;

    public TestPackBuilder WithProduct(ProductInfo product)
    {
        _product = product;
        return this;
    }

    public TestPackBuilder WithSigning(string method)
    {
        _signing = new SigningInfo(method, null);
        return this;
    }

    public TestPackBuilder Add(string path, string kind, string content) => Add(path, kind, Encoding.UTF8.GetBytes(content));

    public TestPackBuilder Add(string path, string kind, byte[] content)
    {
        _files[path] = (kind, content);
        return this;
    }

    public TestPackBuilder AddFixture(string path, string kind, params string[] fixture) =>
        Add(path, kind, File.ReadAllBytes(TestPaths.Fixture(fixture)));

    public TestPackBuilder AddShared(string path, string kind, string name) =>
        Add(path, kind, File.ReadAllBytes(TestPaths.Conformance("shared", name)));

    public PackContents Build(string rulesVersion = "test")
    {
        var files = new SortedDictionary<string, (string Kind, byte[] Bytes)>(_files, StringComparer.Ordinal)
        {
            [PackContents.ProductPath] = (EvidenceKinds.Product, CraTrailJson.Serialize(_product)),
        };
        var manifest = new PackManifest(
            PackManifest.CurrentSchemaVersion,
            new GeneratorInfo("cra-trail", "test"),
            rulesVersion,
            new ProductRef(_product.Name, _product.Version, _product.Purl),
            null,
            _signing,
            "2026-10-15T00:00:00Z",
            [],
            [.. files.Select(f => new PackFile(f.Key, f.Value.Kind, MediaTypes.For(f.Key), f.Value.Bytes.LongLength, PackHashing.Sha256Hex(f.Value.Bytes)))]);
        return PackContents.Create(manifest, files.ToDictionary(f => f.Key, f => f.Value.Bytes, StringComparer.Ordinal));
    }

    public EvaluationContext Context(RuleSet? rules = null) => new(Build(), rules ?? TestRules.Minimal);
}
