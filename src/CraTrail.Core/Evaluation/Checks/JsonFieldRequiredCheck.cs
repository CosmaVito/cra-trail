using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class JsonFieldRequiredCheck : ICheck
{
    public string Type => CheckTypes.JsonFieldRequired;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context) => throw new NotImplementedException();
}
