using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation;

public interface ICheck
{
    string Type { get; }

    CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context);
}
