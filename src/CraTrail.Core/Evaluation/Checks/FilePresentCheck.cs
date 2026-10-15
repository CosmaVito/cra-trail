using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class FilePresentCheck : ICheck
{
    public string Type => CheckTypes.FilePresent;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context) => throw new NotImplementedException();
}
