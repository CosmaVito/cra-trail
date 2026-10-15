using System.Text.Json;
using System.Text.Json.Nodes;
using CraTrail.Core.Json;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class JsonFieldRequiredCheck : ICheck
{
    public string Type => CheckTypes.JsonFieldRequired;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(context);
        var kind = check.GetString("kind") ?? "";
        var files = context.FilesOfKind(kind);
        if (files.Count == 0)
            return CheckOutcome.Missing(check, $"no file of kind {kind}");

        var profile = context.Rules.Profiles[check.GetString("profile") ?? ""];
        var details = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var document = context.ReadJson(file);
                foreach (var field in profile.Fields)
                    details.AddRange(Failures(field, document));
            }
        }
        catch (EvidenceFormatException ex)
        {
            return CheckOutcome.Fail(check, [$"invalid evidence: {ex.Path}"]);
        }

        return details.Count > 0 ? CheckOutcome.Fail(check, details) : CheckOutcome.Pass(check);
    }

    private static List<string> Failures(FieldRule field, JsonNode document)
    {
        if (field.Each is null)
            return Satisfied(field, document) ? [] : [field.Id];

        var elements = field.Each == EachScopes.AllComponents
            ? ComponentWalker.AllComponents(document)
            : ComponentWalker.Components(document);
        var failures = new List<string>();
        for (var i = 0; i < elements.Count; i++)
        {
            if (field.SkipAssemblies && ComponentWalker.IsAssembly(elements[i]))
                continue;
            if (!Satisfied(field, elements[i]))
                failures.Add($"{field.Id}: {ComponentWalker.Reference(elements[i], i)}");
        }
        return failures;
    }

    private static bool Satisfied(FieldRule field, JsonNode node)
    {
        var values = field.Paths.SelectMany(path => JsonSelect.Select(node, path)).ToList();
        var expected = field.Require.Values;
        return field.Require.Kind switch
        {
            FieldRequirementKinds.Present => values.Count > 0,
            FieldRequirementKinds.NonEmpty => values.Any(HasContent),
            FieldRequirementKinds.Absent => values.Count == 0,
            FieldRequirementKinds.Contains => values.Any(v => JsonAccess.Text(v) == expected[0]),
            FieldRequirementKinds.OneOf => values.Count > 0 && values.All(v => JsonAccess.Text(v) is { } text && expected.Contains(text)),
            _ => throw new InvalidOperationException($"Unknown field requirement '{field.Require.Kind}'."),
        };
    }

    private static bool HasContent(JsonNode value) => value switch
    {
        JsonArray array => array.Count > 0,
        JsonObject obj => obj.Count > 0,
        _ => value.GetValueKind() switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetValue<string>()),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => true,
            _ => false,
        },
    };
}
