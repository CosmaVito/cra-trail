using CraTrail.Core.Evidence;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class ScanThresholdCheck : ICheck
{
    public string Type => CheckTypes.ScanThreshold;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(context);
        var kind = check.GetString("kind") ?? "";
        if (context.FilesOfKind(kind).Count == 0)
            return CheckOutcome.Missing(check, $"no file of kind {kind}");

        IReadOnlyList<SarifResult> results;
        try
        {
            results = context.SarifResults(kind);
        }
        catch (EvidenceFormatException ex)
        {
            return CheckOutcome.Fail(check, [$"invalid evidence: {ex.Path}"]);
        }

        var maxResults = check.GetNumber("max-results") ?? 0;
        var levels = check.GetStrings("levels");
        var minSeverity = check.GetNumber("min-security-severity");
        var counted = results
            .Where(r => levels.Count == 0 || levels.Contains(r.Level))
            .Where(r => minSeverity is null || r.SecuritySeverity >= minSeverity)
            .ToList();

        return counted.Count > maxResults
            ? CheckOutcome.Fail(check, counted.Select(Describe))
            : CheckOutcome.Pass(check);
    }

    private static string Describe(SarifResult result)
    {
        var where = $"{result.RuleId} {result.Uri ?? "-"}";
        return result.StartLine is int line ? $"{where}:{line}" : where;
    }
}
