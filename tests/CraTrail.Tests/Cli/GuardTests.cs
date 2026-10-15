using CraTrail.Cli;

namespace CraTrail.Tests.Cli;

public class GuardTests
{
    [Fact]
    public void Unexpected_errors_exit_with_2_and_print_only_the_message()
    {
        using var err = new StringWriter { NewLine = "\n" };
        var io = new CliIO(TextWriter.Null, err, _ => null, ".");

        Assert.Equal(ExitCodes.InputError, Guard.Run(io, () => throw new InvalidOperationException("boom")));
        Assert.Equal("error: boom\n", err.ToString());
    }
}
