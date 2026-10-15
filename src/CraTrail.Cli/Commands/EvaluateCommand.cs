using System.CommandLine;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Pack;

namespace CraTrail.Cli.Commands;

internal static class EvaluateCommand
{
    public static Command Create(CliIO io)
    {
        var pack = new Argument<string>("pack") { Description = "The evidence pack zip." };
        var rules = new Option<string?>("--rules") { Description = "Folder with crosswalk.yaml and profiles. Defaults to the rules shipped with the tool." };
        var output = new Option<string?>("--out") { Description = "Where to write the re-evaluated pack. Defaults to replacing the input." };
        var command = new Command("evaluate", "Re-evaluate an evidence pack, for example with newer rules.") { pack, rules, output };
        command.SetAction(parse => Guard.Run(io, () => Run(io, parse.GetValue(pack)!, parse.GetValue(rules), parse.GetValue(output))));
        return command;
    }

    internal static int Run(CliIO io, string packPath, string? rulesDirectory, string? outPath)
    {
        var (pack, _) = PackLoader.Load(io, packPath);
        if (pack is null)
            return ExitCodes.VerificationFailed;

        var updated = PackBuilder.Reevaluate(pack, RulesSource.Load(rulesDirectory, io), Evaluator.CreateDefault().Evaluate);
        var target = io.Resolve(outPath ?? packPath);
        var temporary = target + ".tmp";
        using (var file = File.Create(temporary))
            PackArchive.Write(updated, file);
        File.Move(temporary, target, overwrite: true);

        var gate = updated.ReadEvaluation()!.Gate;
        io.Out.WriteLine($"wrote {outPath ?? packPath}: rules {updated.Manifest.RulesVersion}, gate {(gate.Passed ? "passed" : "blocked")}");
        return ExitCodes.Ok;
    }
}
