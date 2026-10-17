using System.Globalization;
using System.Text;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Pack;
using CraTrail.Core.Product;

namespace CraTrail.Core.Reporting;

// Everything in the summary comes from the pack, which anyone can write, evaluation.json included. Ids and details go
// in code spans, the rest is escaped, and nothing keeps a line break: no input can add a heading, a link or HTML.
public static class MarkdownSummary
{
    private const int MaxDetails = 20;

    public static string Render(EvaluationResult result, PackManifest manifest, ProductInfo product)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(product);
        var gate = result.Gate;
        var text = new StringBuilder();

        text.Append("## cra-trail gate: ").Append(gate.Passed ? "passed" : "blocked").Append("\n\n");
        text.Append(Text(manifest.Product.Name)).Append(' ').Append(Text(manifest.Product.Version))
            .Append(" · rules ").Append(Text(result.RulesVersion));
        if (!gate.Passed)
            text.Append(" · ").Append(Count(gate.Blocking.Count, "blocking check", "blocking checks"));
        text.Append("\n\n");

        text.Append("Gate checks: ").Append(gate.Required.Count == 0 ? "none" : List(gate.Required)).Append(".\n");
        // The gate policy travels in product.json; a reviewer must see where cra.yml moved it away from the rules.
        var changes = new List<string>();
        if (product.GateExclude.Count > 0)
            changes.Add("Left out of the gate by cra.yml: " + List(product.GateExclude));
        if (product.GateInclude.Count > 0)
            changes.Add("Added to the gate by cra.yml: " + List(product.GateInclude));
        if (product.CheckOverrides.Count > 0)
            changes.Add("Settings changed by cra.yml: " + List([.. product.CheckOverrides.Keys]));
        if (changes.Count > 0)
            text.Append('\n').Append(string.Concat(changes.Select(change => "- " + change + "\n")));
        text.Append('\n');

        if (!gate.Passed)
        {
            text.Append("### Blocking checks\n\n");
            foreach (var block in gate.Blocking)
            {
                text.Append("**").Append(Code(block.Check)).Append("** (").Append(Text(block.Status)).Append(")\n");
                foreach (var detail in block.Details.Take(MaxDetails))
                    text.Append("- ").Append(Code(detail)).Append('\n');
                if (block.Details.Count > MaxDetails)
                    text.Append("- and ").Append(Number(block.Details.Count - MaxDetails)).Append(" more\n");
                if (block.Hint is not null)
                    text.Append("\nFix: ").Append(Text(block.Hint)).Append('\n');
                text.Append('\n');
            }
        }

        text.Append("### Annex I\n\n| Point | Requirement | Coverage | Result |\n|---|---|---|---|\n");
        foreach (var requirement in result.Requirements)
        {
            text.Append("| ").Append(Text(requirement.Point))
                .Append(" | ").Append(Text(requirement.Title))
                .Append(" | ").Append(Text(requirement.Coverage))
                .Append(" | ").Append(Text(requirement.Status))
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

    private static string List(IEnumerable<string> ids) => string.Join(", ", ids.Select(Code));

    // A code span shows its content as typed. Its fence is one backtick longer than the longest run inside it.
    private static string Code(string value)
    {
        var line = OneLine(value);
        var longest = 0;
        var run = 0;
        foreach (var c in line)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        var fence = new string('`', longest + 1);
        var pad = line.StartsWith('`') || line.EndsWith('`') ? " " : "";
        return fence + pad + line + pad + fence;
    }

    private static string Text(string value)
    {
        var text = new StringBuilder();
        foreach (var c in OneLine(value))
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '#' or '|' or '~' or '!' or '&')
                text.Append('\\');
            text.Append(c);
        }
        return text.ToString();
    }

    private static string OneLine(string value) =>
        string.Concat(value.Select(c => char.IsControl(c) || c is '\x2028' or '\x2029' ? ' ' : c));

    private static string Count(int count, string one, string many) => $"{Number(count)} {(count == 1 ? one : many)}";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
