namespace CraTrail.Core.Pack;

public sealed record PackManifest(
    string SchemaVersion,
    GeneratorInfo Generator,
    string RulesVersion,
    ProductRef Product,
    SourceInfo? Source,
    SigningInfo? Signing,
    string CreatedAt,
    IReadOnlyList<ToolInfo> Tools,
    IReadOnlyList<PackFile> Files)
{
    public const string CurrentSchemaVersion = "0.1";
    public const string FileName = "manifest.json";
}

public sealed record GeneratorInfo(string Name, string Version);

public sealed record ProductRef(string Name, string Version, string? Purl);

public sealed record SourceInfo(string? Repository, string? Commit, string? Ref, string? WorkflowRef, string? RunId);

public sealed record SigningInfo(string Method, string? SignerWorkflow);

public sealed record ToolInfo(string Name, string Version);

public sealed record PackFile(string Path, string Kind, string MediaType, long Size, string Sha256);
