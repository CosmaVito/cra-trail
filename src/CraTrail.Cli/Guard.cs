using System.Text.Json;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Pack;
using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Cli;

internal static class Guard
{
    public static int Run(CliIO io, Func<int> action)
    {
        try
        {
            return action();
        }
        catch (PackFormatException ex)
        {
            io.Err.WriteLine($"error: {ex.Message}");
            return ExitCodes.VerificationFailed;
        }
        catch (Exception ex) when (ex is ConfigException or RuleSetException or PackBuildException or EvidenceFormatException
            or IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            io.Err.WriteLine($"error: {ex.Message}");
            return ExitCodes.InputError;
        }
        catch (Exception ex)
        {
            // Anything else would reach System.CommandLine and exit with 1, which means "gate blocked".
            io.Err.WriteLine($"error: {ex.Message}");
            return ExitCodes.InputError;
        }
    }
}
