using System.Text.Json.Nodes;

namespace CraTrail.Core.Product;

public sealed record ProductInfo(
    string Name,
    string Version,
    string? Purl,
    string? SupportEnd,
    string? CvdContact,
    string? SecurityTxt,
    IReadOnlyDictionary<string, string> UserInfo,
    IReadOnlyDictionary<string, string> Attested,
    IReadOnlyDictionary<string, string> NotApplicable,
    IReadOnlyDictionary<string, JsonObject> CheckOverrides,
    IReadOnlyList<string> GateInclude,
    IReadOnlyList<string> GateExclude);

public sealed record CraConfig(ProductInfo Product, IReadOnlyList<string> AttestedSources);

public sealed class ConfigException(IReadOnlyList<string> errors)
    : Exception("Invalid configuration:\n" + string.Join("\n", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
