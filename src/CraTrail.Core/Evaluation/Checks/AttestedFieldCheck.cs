using System.Text.Json;
using System.Text.Json.Nodes;
using CraTrail.Core.Json;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class AttestedFieldCheck : ICheck
{
    public string Type => CheckTypes.AttestedField;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(context);
        var field = check.GetString("field") ?? "";
        var dot = field.IndexOf('.', StringComparison.Ordinal);
        var root = dot < 0 ? null : field[..dot] switch
        {
            "product" => context.ProductJson,
            "manifest" => context.ManifestJson,
            _ => null,
        };

        return root is not null && JsonSelect.Select(root, field[(dot + 1)..]).Any(IsSet)
            ? CheckOutcome.Pass(check)
            : CheckOutcome.Missing(check, $"{field} is not set");
    }

    private static bool IsSet(JsonNode value) => value.GetValueKind() switch
    {
        JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetValue<string>()),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => true,
        _ => false,
    };
}
