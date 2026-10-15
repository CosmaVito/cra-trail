using CraTrail.Core.Rules;

namespace CraTrail.Core.Evaluation.Checks;

public sealed class FilePresentCheck : ICheck
{
    public string Type => CheckTypes.FilePresent;

    public CheckOutcome Evaluate(CheckDefinition check, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(context);
        var kind = check.GetString("kind") ?? "";
        return context.FilesOfKind(kind).Count > 0
            ? CheckOutcome.Pass(check)
            : CheckOutcome.Missing(check, $"no file of kind {kind}");
    }
}
