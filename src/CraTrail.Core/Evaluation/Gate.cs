using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation;

public static class Gate
{
    public static GateOutcome Decide(RuleSet rules, ProductInfo product, IReadOnlyList<CheckOutcome> checks)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(checks);

        var required = rules.Gate.Where(id => !product.GateExclude.Contains(id)).ToList();
        foreach (var id in product.GateInclude)
        {
            if (!required.Contains(id) && !product.GateExclude.Contains(id))
                required.Add(id);
        }

        var blocking = new List<GateBlock>();
        foreach (var id in required)
        {
            var outcome = checks.FirstOrDefault(c => c.Id == id);
            if (outcome is null)
                blocking.Add(new GateBlock(id, Statuses.Missing, ["unknown check"], null));
            else if (outcome.Status != Statuses.Pass)
                blocking.Add(new GateBlock(id, outcome.Status, outcome.Details, rules.FindCheck(id)?.Hint));
        }
        return new GateOutcome(blocking.Count == 0, required, blocking);
    }
}
