using System.Globalization;
using System.Text;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Pack;

namespace CraTrail.Core.Reporting;

public static class MarkdownSummary
{
    private const int MaxDetails = 20;

    public static string Render(EvaluationResult result, PackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(manifest);
        var gate = result.Gate;
        var text = new StringBuilder();

        text.Append("## cra-trail gate: ").Append(gate.Passed ? "passed" : "blocked").Append("\n\n");
        text.Append(manifest.Product.Name).Append(' ').Append(manifest.Product.Version)
            .Append(" · rules ").Append(result.RulesVersion);
        if (!gate.Passed)
            text.Append(" · ").Append(Count(gate.Blocking.Count, "blocking check", "blocking checks"));
        text.Append("\n\n");

        if (!gate.Passed)
        {
            text.Append("### Blocking checks\n\n");
            foreach (var block in gate.Blocking)
            {
                text.Append("**").Append(block.Check).Append("** (").Append(block.Status).Append(")\n");
                foreach (var detail in block.Details.Take(MaxDetails))
                    text.Append("- ").Append(detail).Append('\n');
                if (block.Details.Count > MaxDetails)
                    text.Append("- and ").Append(Number(block.Details.Count - MaxDetails)).Append(" more\n");
                if (block.Hint is not null)
                    text.Append("\nFix: ").Append(block.Hint).Append('\n');
                text.Append('\n');
            }
        }

        text.Append("### Annex I\n\n| Point | Requirement | Coverage | Result |\n|---|---|---|---|\n");
        foreach (var requirement in result.Requirements)
        {
            text.Append("| ").Append(Cell(requirement.Point))
                .Append(" | ").Append(Cell(requirement.Title))
                .Append(" | ").Append(requirement.Coverage)
                .Append(" | ").Append(requirement.Status)
                .Append(" |\n");
        }

        var totals = result.Totals;
        text.Append('\n')
            .Append(Number(totals.Pass)).Append(" pass · ")
            .Append(Number(totals.Attested)).Append(" attested · ")
            .Append(Number(totals.Missing)).Append(" missing · ")
            .Append(Number(totals.Fail)).Append(" fail · ")
            .Append(Number(totals.NotApplicable)).Append(" not applicable\n\n");
        text.Append("Evidence for review, not a conformity assessment.\n");
        return text.ToString();
    }

    private static string Cell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Count(int count, string one, string many) => $"{Number(count)} {(count == 1 ? one : many)}";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
