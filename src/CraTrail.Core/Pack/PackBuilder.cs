using System.Text.Json;
using System.Text.Json.Nodes;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Evidence;
using CraTrail.Core.Json;
using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Pack;

public sealed record PackInput(string Kind, string FileName, byte[] Content, string? RelativePath = null);

public sealed record PackRequest(
    ProductInfo Product,
    IReadOnlyList<PackInput> Inputs,
    IReadOnlyList<ToolInfo> Tools,
    SourceInfo? Source,
    SigningInfo? Signing,
    DateTimeOffset CreatedAt,
    string GeneratorVersion);

public delegate EvaluationResult EvaluateFunc(PackContents pack, RuleSet rules);

public sealed class PackBuildException(string message) : Exception(message);

public static class PackBuilder
{
    public static PackContents Build(PackRequest request, RuleSet rules, EvaluateFunc evaluate)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(evaluate);

        var files = new SortedDictionary<string, (string Kind, byte[] Bytes)>(StringComparer.Ordinal);
        var vexDocuments = new List<JsonNode>();
        foreach (var input in request.Inputs)
        {
            var path = TargetPath(input);
            var node = Validate(input);
            if (input.Kind == EvidenceKinds.Vex)
            {
                vexDocuments.Add(node!);
                continue;
            }
            // Any SARIF can quote source lines, and the pack is meant to be published.
            var content = input.Kind is EvidenceKinds.SecretsSarif or EvidenceKinds.SastSarif
                ? CraTrailJson.SerializeNode(Sarif.Redact(node!))
                : input.Content;
            Add(files, path, input.Kind, content);
        }

        if (vexDocuments.Count > 0)
        {
            var id = $"urn:cra-trail:vex:{Uri.EscapeDataString(request.Product.Name)}:{Uri.EscapeDataString(request.Product.Version)}";
            Add(files, "vex/product.openvex.json", EvidenceKinds.Vex,
                CraTrailJson.SerializeNode(OpenVex.Merge(vexDocuments, id, "cra-trail", request.CreatedAt)));
        }

        foreach (var path in request.Product.Attested.Values.Where(p => !files.ContainsKey(p)))
            throw new PackBuildException($"Attested document is missing from the inputs: {path}");
        Add(files, PackContents.ProductPath, EvidenceKinds.Product, CraTrailJson.Serialize(request.Product));

        var header = new PackManifest(
            PackManifest.CurrentSchemaVersion,
            new GeneratorInfo("cra-trail", request.GeneratorVersion),
            rules.Version,
            new ProductRef(request.Product.Name, request.Product.Version, request.Product.Purl),
            request.Source,
            request.Signing,
            Timestamps.Format(request.CreatedAt),
            [.. request.Tools.OrderBy(t => t.Name, StringComparer.Ordinal)],
            []);
        return Finish(header, files, rules, evaluate);
    }

    public static PackContents Reevaluate(PackContents pack, RuleSet rules, EvaluateFunc evaluate)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(evaluate);

        var files = new SortedDictionary<string, (string Kind, byte[] Bytes)>(StringComparer.Ordinal);
        foreach (var file in pack.Manifest.Files.Where(f => f.Path != PackContents.EvaluationPath))
            files[file.Path] = (file.Kind, pack.Files[file.Path]);
        return Finish(pack.Manifest with { RulesVersion = rules.Version, Files = [] }, files, rules, evaluate);
    }

    private static PackContents Finish(
        PackManifest header,
        SortedDictionary<string, (string Kind, byte[] Bytes)> files,
        RuleSet rules,
        EvaluateFunc evaluate)
    {
        var evaluation = evaluate(Assemble(header, files), rules);
        files[PackContents.EvaluationPath] = (EvidenceKinds.Evaluation, CraTrailJson.Serialize(evaluation));
        return Assemble(header, files);
    }

    private static PackContents Assemble(PackManifest header, SortedDictionary<string, (string Kind, byte[] Bytes)> files)
    {
        var entries = files
            .Select(f => new PackFile(f.Key, f.Value.Kind, MediaTypes.For(f.Key), f.Value.Bytes.LongLength, PackHashing.Sha256Hex(f.Value.Bytes)))
            .ToList();
        return PackContents.Create(header with { Files = entries }, files.ToDictionary(f => f.Key, f => f.Value.Bytes, StringComparer.Ordinal));
    }

    private static void Add(SortedDictionary<string, (string Kind, byte[] Bytes)> files, string path, string kind, byte[] content)
    {
        if (!files.TryAdd(path, (kind, content)))
            throw new PackBuildException($"Two inputs map to the same pack path: {path}");
    }

    private static string TargetPath(PackInput input) => input.Kind switch
    {
        EvidenceKinds.ProductSbom => "sbom/product.cdx.json",
        EvidenceKinds.Sbom => "sbom/" + SafeName(input.FileName),
        EvidenceKinds.Vex => "vex/product.openvex.json",
        EvidenceKinds.Grype => "scans/grype.json",
        EvidenceKinds.SastSarif => "scans/" + SafeName(input.FileName),
        EvidenceKinds.SecretsSarif => "scans/secrets.sarif",
        EvidenceKinds.CvdPolicy => "policy/" + SafeName(input.FileName),
        EvidenceKinds.AttestedDocument => "attested/" + RelativeDocumentPath(input.RelativePath ?? input.FileName),
        EvidenceKinds.TestResults or EvidenceKinds.SbomQuality or EvidenceKinds.NuGetAudit or EvidenceKinds.NpmAuditSignatures =>
            "evidence/" + SafeName(input.FileName),
        _ => throw new PackBuildException($"Unknown evidence kind '{input.Kind}'."),
    };

    private static JsonNode? Validate(PackInput input)
    {
        var needsJson = input.Kind is EvidenceKinds.ProductSbom or EvidenceKinds.Sbom or EvidenceKinds.Vex
            or EvidenceKinds.Grype or EvidenceKinds.SastSarif or EvidenceKinds.SecretsSarif;
        if (!needsJson)
            return null;
        try
        {
            var node = JsonNode.Parse(input.Content) ?? throw new FormatException("The file contains JSON null.");
            switch (input.Kind)
            {
                case EvidenceKinds.ProductSbom or EvidenceKinds.Sbom:
                    if (JsonAccess.Text(JsonAccess.Child(node, "bomFormat")) != "CycloneDX")
                        throw new FormatException("Expected a CycloneDX JSON document.");
                    break;
                case EvidenceKinds.Vex:
                    OpenVex.ParseStatements(node);
                    break;
                case EvidenceKinds.Grype:
                    Grype.Parse(node);
                    break;
                default:
                    Sarif.ParseResults(node);
                    break;
            }
            return node;
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new PackBuildException($"{input.FileName} is not a valid {input.Kind} input: {ex.Message}");
        }
    }

    private static string SafeName(string fileName)
    {
        var name = fileName.Replace('\\', '/').Split('/')[^1];
        if (name is "" or "." or "..")
            throw new PackBuildException($"'{fileName}' is not a usable file name.");
        return name;
    }

    private static string RelativeDocumentPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        if (!PackVerifier.IsSafePath(normalized))
            throw new PackBuildException($"'{path}' is not a relative path inside the repository.");
        return normalized;
    }
}
