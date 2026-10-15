using System.CommandLine;
using CraTrail.Core.Reporting;

namespace CraTrail.Cli.Commands;

internal static class GateCommand
{
    public static Command Create(CliIO io)
    {
        var pack = new Argument<string>("pack") { Description = "The evidence pack zip." };
        var summary = new Option<string?>("--summary") { Description = "Append the Markdown summary to this file. Defaults to GITHUB_STEP_SUMMARY when it is set." };
        var command = new Command("gate", "Decide whether the release passes and print the summary.") { pack, summary };
        command.SetAction(parse => Guard.Run(io, () => Run(io, parse.GetValue(pack)!, parse.GetValue(summary))));
        return command;
    }

    internal static int Run(CliIO io, string packPath, string? summaryPath)
    {
        var (pack, _) = PackLoader.Load(io, packPath);
        if (pack is null)
            return ExitCodes.VerificationFailed;
        var evaluation = pack.ReadEvaluation();
        if (evaluation is null)
        {
            io.Err.WriteLine("error: the pack has no evaluation.json.");
            return ExitCodes.VerificationFailed;
        }

        var markdown = MarkdownSummary.Render(evaluation, pack.Manifest);
        io.Out.Write(markdown);
        var target = summaryPath ?? io.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (!string.IsNullOrWhiteSpace(target))
            File.AppendAllText(io.Resolve(target), markdown);
        return evaluation.Gate.Passed ? ExitCodes.Ok : ExitCodes.GateBlocked;
    }
}
