using System.CommandLine;
using System.Reflection;
using CraTrail.Cli.Attestation;
using CraTrail.Cli.Commands;

namespace CraTrail.Cli;

internal static class CliApp
{
    public static int Run(string[] args, CliIO io) => Run(args, io, new GhAttestationVerifier());

    public static int Run(string[] args, CliIO io, IAttestationVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(io);
        var root = new RootCommand("Builds, evaluates and verifies Cyber Resilience Act evidence packs.");
        root.Subcommands.Add(InitCommand.Create(io));
        root.Subcommands.Add(PackCommand.Create(io));
        root.Subcommands.Add(EvaluateCommand.Create(io));
        root.Subcommands.Add(GateCommand.Create(io));
        root.Subcommands.Add(VerifyCommand.Create(io, verifier));

        var parse = root.Parse(args);
        if (parse.Errors.Count > 0)
        {
            foreach (var error in parse.Errors)
                io.Err.WriteLine($"error: {error.Message}");
            return ExitCodes.InputError;
        }
        return parse.Invoke(new InvocationConfiguration { Output = io.Out, Error = io.Err });
    }

    public static string ToolVersion()
    {
        var version = typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}
