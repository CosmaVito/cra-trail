using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation;

public static class Gate
{
    public static GateOutcome Decide(RuleSet rules, ProductInfo product, IReadOnlyList<CheckOutcome> checks) =>
        throw new NotImplementedException();
}
