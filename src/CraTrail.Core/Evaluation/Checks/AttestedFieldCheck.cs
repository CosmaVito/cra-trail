using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class AttestedFieldCheck : ICheck
{
    public string Type => CheckTypes.AttestedField;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context) => throw new NotImplementedException();
}
