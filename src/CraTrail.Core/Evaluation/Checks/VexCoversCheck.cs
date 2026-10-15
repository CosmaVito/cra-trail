using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class VexCoversCheck : ICheck
{
    public string Type => CheckTypes.VexCovers;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context) => throw new NotImplementedException();
}
