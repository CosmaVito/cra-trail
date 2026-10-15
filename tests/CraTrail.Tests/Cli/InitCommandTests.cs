using CraTrail.Cli;
using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Cli;

public class InitCommandTests
{
    [Fact]
    public void Creates_the_starting_files()
    {
        using var run = new CliRun();

        Assert.Equal(ExitCodes.Ok, run.Run("init"));

        Assert.True(File.Exists(run.Path("cra.yml")));
        Assert.True(File.Exists(run.Path("SECURITY.md")));
        Assert.True(File.Exists(run.Path("vex", "README.md")));
        var output = run.Out.ToString();
        Assert.Contains("created  cra.yml", output, StringComparison.Ordinal);
        Assert.Contains("created  vex/README.md", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_existing_files_unless_forced()
    {
        using var run = new CliRun();
        File.WriteAllText(run.Path("cra.yml"), "mine");

        run.Run("init");
        Assert.Equal("mine", File.ReadAllText(run.Path("cra.yml")));
        Assert.Contains("kept     cra.yml (already exists)", run.Out.ToString(), StringComparison.Ordinal);

        run.Run("init", "--force");
        Assert.Equal(Templates.Read("cra.yml"), File.ReadAllText(run.Path("cra.yml")));
    }

    [Fact]
    public void The_template_configuration_loads_with_the_default_rules()
    {
        var config = CraConfigLoader.Load(Templates.Read("cra.yml"), "1.0.0", DefaultRules.Load(), _ => true);
        Assert.Equal("2031-12-31", config.Product.SupportEnd);
        Assert.Empty(config.Product.GateExclude);
    }

    [Fact]
    public void Unknown_commands_and_missing_commands_exit_with_2()
    {
        using var run = new CliRun();
        Assert.Equal(ExitCodes.InputError, run.Run("nope"));
        Assert.Equal(ExitCodes.InputError, run.Run());
        Assert.Contains("error:", run.Err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Help_exits_with_0()
    {
        using var run = new CliRun();
        Assert.Equal(ExitCodes.Ok, run.Run("--help"));
        Assert.Contains("init", run.Out.ToString(), StringComparison.Ordinal);
    }
}
