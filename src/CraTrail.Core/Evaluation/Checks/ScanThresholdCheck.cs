using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class ScanThresholdCheck : ICheck
{
    public string Type => CheckTypes.ScanThreshold;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context) => throw new NotImplementedException();
}
