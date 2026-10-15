using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation;

public sealed record EvaluationResult(
    string SchemaVersion,
    string RulesVersion,
    IReadOnlyList<CheckOutcome> Checks,
    IReadOnlyList<RequirementOutcome> Requirements,
    GateOutcome Gate,
    StatusTotals Totals)
{
    public const string CurrentSchemaVersion = "0.1";
}

public sealed record CheckOutcome(string Id, string Type, string Status, IReadOnlyList<string> Details)
{
    public static CheckOutcome Pass(CheckDefinition check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(check.Id, check.Type, Statuses.Pass, []);
    }

    public static CheckOutcome Missing(CheckDefinition check, string detail)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(check.Id, check.Type, Statuses.Missing, [detail]);
    }

    public static CheckOutcome Fail(CheckDefinition check, IEnumerable<string> details)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(check.Id, check.Type, Statuses.Fail, [.. details.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }
}

public sealed record RequirementOutcome(
    string Id,
    string Point,
    string Title,
    string Coverage,
    string Status,
    IReadOnlyList<string> Checks,
    string? Attestation,
    string? Reason,
    IReadOnlyList<string> Details);

public sealed record GateOutcome(bool Passed, IReadOnlyList<string> Required, IReadOnlyList<GateBlock> Blocking);

public sealed record GateBlock(string Check, string Status, IReadOnlyList<string> Details, string? Hint);

public sealed record StatusTotals(int Pass, int Fail, int Attested, int Missing, int NotApplicable);
