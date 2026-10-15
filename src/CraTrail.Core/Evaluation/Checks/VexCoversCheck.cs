using CraTrail.Core.Evidence;
using CraTrail.Core.Json;
using CraTrail.Core.Pack;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class VexCoversCheck : ICheck
{
    public string Type => CheckTypes.VexCovers;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(context);
        if (context.FilesOfKind(EvidenceKinds.Grype).Count == 0)
            return CheckOutcome.Missing(check, $"no file of kind {EvidenceKinds.Grype}");

        var select = check.GetObject("select");
        var kevOnly = JsonAccess.Bool(JsonAccess.Child(select, "kev")) == true;
        var minCvss = JsonAccess.Number(JsonAccess.Child(select, "min-cvss"));

        List<string> uncovered;
        try
        {
            uncovered = [.. context.Findings
                .Where(f => (!kevOnly || f.Kev) && (minCvss is null || f.EffectiveScore >= minCvss))
                .Where(f => !IsCovered(context.Vex.Resolve(f.Ids, f.Purl)))
                .Select(f => f.Purl is null ? $"{f.Id} {f.Name}@{f.Version}" : $"{f.Id} {f.Purl}")];
        }
        catch (EvidenceFormatException ex)
        {
            return CheckOutcome.Fail(check, [$"invalid evidence: {ex.Path}"]);
        }

        return uncovered.Count > 0 ? CheckOutcome.Fail(check, uncovered) : CheckOutcome.Pass(check);
    }

    private static bool IsCovered(VexStatement? statement) => statement?.Status is "not_affected" or "fixed";
}
