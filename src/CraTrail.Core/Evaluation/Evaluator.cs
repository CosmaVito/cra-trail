using CraTrail.Core.Evaluation.Checks;
using CraTrail.Core.Pack;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation;

public sealed class Evaluator(IEnumerable<ICheck> checks)
{
    private readonly Dictionary<string, ICheck> _checks = checks.ToDictionary(c => c.Type, StringComparer.Ordinal);

    public IReadOnlyCollection<ICheck> Checks => _checks.Values;

    public static Evaluator CreateDefault() => new(
    [
        new FilePresentCheck(),
        new JsonFieldRequiredCheck(),
        new ScanThresholdCheck(),
        new VexCoversCheck(),
        new AttestedFieldCheck(),
    ]);

    public EvaluationResult Evaluate(PackContents pack, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(rules);
        var context = new EvaluationContext(pack, rules);

        var outcomes = new List<CheckOutcome>();
        foreach (var check in rules.Checks)
        {
            var definition = check.WithOverrides(pack.Product.CheckOverrides.GetValueOrDefault(check.Id));
            if (!_checks.TryGetValue(definition.Type, out var implementation))
                throw new InvalidOperationException($"No implementation for check type '{definition.Type}'.");
            outcomes.Add(implementation.Evaluate(definition, context));
        }
        outcomes.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        var byId = outcomes.ToDictionary(o => o.Id, StringComparer.Ordinal);
        var requirements = rules.Requirements.Select(r => Assess(r, pack, byId)).ToList();
        var totals = new StatusTotals(
            requirements.Count(r => r.Status == Statuses.Pass),
            requirements.Count(r => r.Status == Statuses.Fail),
            requirements.Count(r => r.Status == Statuses.Attested),
            requirements.Count(r => r.Status == Statuses.Missing),
            requirements.Count(r => r.Status == Statuses.NotApplicable));

        return new EvaluationResult(
            EvaluationResult.CurrentSchemaVersion,
            rules.Version,
            outcomes,
            requirements,
            Gate.Decide(rules, pack.Product, outcomes),
            totals);
    }

    private static RequirementOutcome Assess(Requirement requirement, PackContents pack, Dictionary<string, CheckOutcome> outcomes)
    {
        var result = new RequirementOutcome(
            requirement.Id, requirement.Point, requirement.Title, requirement.Coverage, Statuses.Missing, requirement.Checks, null, null, []);

        if (pack.Product.NotApplicable.TryGetValue(requirement.Id, out var reason))
        {
            return string.IsNullOrWhiteSpace(reason)
                ? result with { Status = Statuses.Fail, Details = ["not-applicable requires a reason"] }
                : result with { Status = Statuses.NotApplicable, Reason = reason };
        }

        var statuses = requirement.Checks.Select(id => outcomes[id].Status).ToList();
        if (statuses.Contains(Statuses.Fail))
            return result with { Status = Statuses.Fail };
        if (statuses.Count > 0 && statuses.All(s => s == Statuses.Pass))
            return result with { Status = Statuses.Pass };

        if (pack.Product.Attested.TryGetValue(requirement.Id, out var path))
        {
            return pack.FilesOfKind(EvidenceKinds.AttestedDocument).Any(f => f.Path == path)
                ? result with { Status = Statuses.Attested, Attestation = path }
                : result with { Details = [$"attested document not in pack: {path}"] };
        }
        return result;
    }
}
