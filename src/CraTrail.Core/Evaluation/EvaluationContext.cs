using System.Text.Json;
using System.Text.Json.Nodes;
using CraTrail.Core.Evidence;
using CraTrail.Core.Pack;
using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation;

public sealed class EvidenceFormatException(string path, string message, Exception? inner = null)
    : Exception($"{path}: {message}", inner)
{
    public string Path { get; } = path;
}

public sealed class EvaluationContext
{
    private readonly Lazy<IReadOnlyList<GrypeReport>> _grype;
    private readonly Lazy<VexIndex> _vex;

    public EvaluationContext(PackContents pack, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(pack);
        Pack = pack;
        Rules = rules;
        ProductJson = JsonNode.Parse(pack.Files[PackContents.ProductPath]) ?? new JsonObject();
        ManifestJson = JsonNode.Parse(pack.ManifestBytes) ?? new JsonObject();
        _grype = new(() => [.. FilesOfKind(EvidenceKinds.Grype).Select(f => Read(f, Grype.Parse))]);
        _vex = new(() =>
        {
            var statements = new List<VexStatement>();
            foreach (var file in FilesOfKind(EvidenceKinds.Vex))
                statements.AddRange(Read(file, document => OpenVex.ParseStatements(document, statements.Count)));
            return new VexIndex(statements);
        });
    }

    public PackContents Pack { get; }

    public RuleSet Rules { get; }

    public ProductInfo Product => Pack.Product;

    public JsonNode ProductJson { get; }

    public JsonNode ManifestJson { get; }

    public IReadOnlyList<Finding> Findings => [.. _grype.Value.SelectMany(r => r.Matches)];

    public IReadOnlyList<Finding> IgnoredFindings => [.. _grype.Value.SelectMany(r => r.Ignored)];

    public VexIndex Vex => _vex.Value;

    public IReadOnlyList<PackFile> FilesOfKind(string kind) => [.. Pack.FilesOfKind(kind)];

    public JsonNode ReadJson(PackFile file) => Read(file, node => node);

    public IReadOnlyList<SarifResult> SarifResults(string kind) =>
        [.. FilesOfKind(kind).SelectMany(f => Read(f, Sarif.ParseResults))];

    private T Read<T>(PackFile file, Func<JsonNode, T> parse)
    {
        try
        {
            var node = JsonNode.Parse(Pack.Files[file.Path]) ?? throw new FormatException("The file contains JSON null.");
            return parse(node);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new EvidenceFormatException(file.Path, ex.Message, ex);
        }
    }
}
