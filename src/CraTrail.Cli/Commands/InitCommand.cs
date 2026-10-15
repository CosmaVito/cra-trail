using System.CommandLine;

namespace CraTrail.Cli.Commands;

internal static class InitCommand
{
    public static Command Create(CliIO io)
    {
        var directory = new Option<string>("--dir") { Description = "Repository root to set up.", DefaultValueFactory = _ => "." };
        var force = new Option<bool>("--force") { Description = "Overwrite files that already exist." };
        var command = new Command("init", "Create cra.yml, a SECURITY.md template and the vex folder.") { directory, force };
        command.SetAction(parse => Guard.Run(io, () => Run(io, parse.GetValue(directory)!, parse.GetValue(force))));
        return command;
    }

    internal static int Run(CliIO io, string directory, bool force)
    {
        var root = io.Resolve(directory);
        Directory.CreateDirectory(Path.Combine(root, "vex"));
        Write(io, root, "cra.yml", Templates.Read("cra.yml"), force);
        Write(io, root, "SECURITY.md", Templates.Read("SECURITY.md"), force);
        Write(io, root, "vex/README.md", Templates.Read("vex-README.md"), force);
        io.Out.WriteLine("Next: fill in cra.yml and SECURITY.md, then run the pipeline.");
        return ExitCodes.Ok;
    }

    private static void Write(CliIO io, string root, string relative, string content, bool force)
    {
        var path = Path.Combine(root, relative);
        if (File.Exists(path) && !force)
        {
            io.Out.WriteLine($"kept     {relative} (already exists)");
            return;
        }
        File.WriteAllText(path, content);
        io.Out.WriteLine($"created  {relative}");
    }
}
