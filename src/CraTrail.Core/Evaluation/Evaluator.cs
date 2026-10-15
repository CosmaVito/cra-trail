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

    public EvaluationResult Evaluate(PackContents pack, RuleSet rules) => throw new NotImplementedException();
}
