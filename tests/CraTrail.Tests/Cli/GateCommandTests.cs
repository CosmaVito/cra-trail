using CraTrail.Cli;

namespace CraTrail.Tests.Cli;

public class GateCommandTests
{
    [Fact]
    public void A_passing_pack_exits_0_and_appends_the_summary()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);
        CliPacks.Pack(run);
        File.WriteAllText(run.Path("summary.md"), "earlier step\n");
        run.Out.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.Ok, run.Run("gate", "out/pack.zip", "--summary", "summary.md"));

        Assert.StartsWith("## cra-trail gate: passed", run.Out.ToString(), StringComparison.Ordinal);
        var summary = File.ReadAllText(run.Path("summary.md"));
        Assert.StartsWith("earlier step\n## cra-trail gate: passed", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blocked_pack_exits_1()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);
        CliPacks.Pack(run, "grype-kev.json");

        Assert.Equal(ExitCodes.GateBlocked, run.Run("gate", "out/pack.zip"));
        Assert.Contains("**`no-kev-without-vex`** (fail)", run.Out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_github_step_summary_is_used_when_set()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);
        CliPacks.Pack(run);
        run.Environment["GITHUB_STEP_SUMMARY"] = run.Path("step-summary.md");

        run.Run("gate", "out/pack.zip");

        Assert.Contains("Evidence for review, not a conformity assessment.", File.ReadAllText(run.Path("step-summary.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_tampered_pack_exits_3()
    {
        using var run = new CliRun();
        CliPacks.WriteRepo(run);
        CliPacks.Pack(run);
        EvaluateCommandTests.Tamper(run.Path("out", "pack.zip"), "evaluation.json");

        Assert.Equal(ExitCodes.VerificationFailed, run.Run("gate", "out/pack.zip"));
    }
}
