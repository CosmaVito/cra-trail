using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Rules;

public class DefaultRulesTests
{
    private static readonly RuleSet Rules = DefaultRules.Load();

    [Fact]
    public void Covers_every_point_of_annex_i_once()
    {
        string[] expected =
        [
            "annex-i/part-i/1", "annex-i/part-i/2a", "annex-i/part-i/2b", "annex-i/part-i/2c", "annex-i/part-i/2d",
            "annex-i/part-i/2e", "annex-i/part-i/2f", "annex-i/part-i/2g", "annex-i/part-i/2h", "annex-i/part-i/2i",
            "annex-i/part-i/2j", "annex-i/part-i/2k", "annex-i/part-i/2l", "annex-i/part-i/2m",
            "annex-i/part-ii/1", "annex-i/part-ii/2", "annex-i/part-ii/3", "annex-i/part-ii/4",
            "annex-i/part-ii/5", "annex-i/part-ii/6", "annex-i/part-ii/7", "annex-i/part-ii/8",
        ];
        Assert.Equal(expected, Rules.Requirements.Select(r => r.Id));
        Assert.Equal("2026.10", Rules.Version);
    }

    [Fact]
    public void Every_requirement_cites_the_regulation() =>
        Assert.All(Rules.Requirements, r => Assert.StartsWith("Reg. (EU) 2024/2847, Annex I, Part ", r.Source, StringComparison.Ordinal));

    [Fact]
    public void The_gate_blocks_on_vulnerabilities_secrets_sbom_and_product_fields() =>
        Assert.Equal(
            new[] { "no-kev-without-vex", "no-critical-without-vex", "no-secrets", "sbom-core-fields", "support-end-set", "cvd-contact-set" },
            Rules.Gate);

    [Fact]
    public void The_bsi_check_is_the_only_informative_one()
    {
        var used = Rules.Gate.Concat(Rules.Requirements.SelectMany(r => r.Checks)).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new[] { "sbom-bsi-fields" }, Rules.Checks.Select(c => c.Id).Where(id => !used.Contains(id)));
        Assert.Equal("bsi-tr03183-2", Rules.FindCheck("sbom-bsi-fields")!.GetString("profile"));
    }

    [Fact]
    public void Configuration_gate_options_find_their_checks()
    {
        Assert.NotNull(Rules.FindCheck(CraConfigLoader.BlockKevCheck));
        Assert.NotNull(Rules.FindCheck(CraConfigLoader.MaxCvssCheck));
        Assert.NotNull(Rules.FindCheck(CraConfigLoader.BsiCheck));
    }

    [Fact]
    public void Both_sbom_profiles_are_loaded()
    {
        Assert.Equal(5, Rules.Profiles["sbom-core"].Fields.Count);
        Assert.Equal(14, Rules.Profiles["bsi-tr03183-2"].Fields.Count);
    }
}
