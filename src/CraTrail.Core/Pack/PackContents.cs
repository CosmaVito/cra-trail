using System.Text.Json;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Json;
using CraTrail.Core.Product;

namespace CraTrail.Core.Pack;

public sealed class PackContents
{
    public const string ProductPath = "product.json";
    public const string EvaluationPath = "evaluation.json";

    public PackContents(PackManifest manifest, byte[] manifestBytes, IReadOnlyDictionary<string, byte[]> files)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(files);
        foreach (var file in manifest.Files.Where(f => !files.ContainsKey(f.Path)))
            throw new PackFormatException($"The manifest lists {file.Path} but the pack does not contain it.");
        var listed = manifest.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var path in files.Keys.Where(p => !listed.Contains(p)))
            throw new PackFormatException($"The pack contains {path} but the manifest does not list it.");
        if (!files.TryGetValue(ProductPath, out var productBytes))
            throw new PackFormatException("The pack has no product.json.");

        try
        {
            Product = CraTrailJson.Deserialize<ProductInfo>(productBytes);
        }
        catch (JsonException ex)
        {
            throw new PackFormatException($"product.json is not valid: {ex.Message}");
        }
        Manifest = manifest;
        ManifestBytes = manifestBytes;
        Files = files;
    }

    public PackManifest Manifest { get; }

    public byte[] ManifestBytes { get; }

    public IReadOnlyDictionary<string, byte[]> Files { get; }

    public ProductInfo Product { get; }

    public static PackContents Create(PackManifest manifest, IReadOnlyDictionary<string, byte[]> files) =>
        new(manifest, CraTrailJson.Serialize(manifest), files);

    public IEnumerable<PackFile> FilesOfKind(string kind) => Manifest.Files.Where(f => f.Kind == kind);

    public EvaluationResult? ReadEvaluation() =>
        Files.TryGetValue(EvaluationPath, out var bytes) ? CraTrailJson.Deserialize<EvaluationResult>(bytes) : null;
}
