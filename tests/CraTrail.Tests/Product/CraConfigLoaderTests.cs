using CraTrail.Core.Product;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Product;

public class CraConfigLoaderTests
{
    private static readonly RuleSet Rules = RuleSetLoader.Load("""
        version: "1"
        gate: [no-critical-without-vex, no-kev-without-vex]
        checks:
          no-critical-without-vex:
            type: vex-covers
            kind: grype
            select:
              min-cvss: 9.0
          no-kev-without-vex:
            type: vex-covers
            kind: grype
            select:
              kev: true
          sbom-bsi-fields:
            type: file-present
            kind: product-sbom
        requirements:
          - id: annex-i/part-i/1
            point: I (1)
            title: Risk
            source: S
            coverage: manual
          - id: annex-i/part-i/2i
            point: I (2)(i)
            title: Other devices
            source: S
            coverage: manual
        """, []);

    private const string Full = """
        product:
          name: CVD Inbox
          purl: pkg:github/CosmaVito/cra-trail
          support-end: 2031-12-31
          cvd-contact: security@example.org
          security-txt: https://example.org/.well-known/security.txt
        user-info:
          intended-purpose: Receive vulnerability reports.
        attested:
          annex-i/part-i/1: docs\risk-assessment.md
        not-applicable:
          annex-i/part-i/2i: No network functions that affect other devices.
        gate:
          max-cvss-without-vex: 7.5
          block-kev-without-vex: false
          require-bsi: true
        """;

    [Fact]
    public void Reads_a_complete_configuration()
    {
        var config = CraConfigLoader.Load(Full, "1.2.0", Rules, path => path == "docs/risk-assessment.md");
        var product = config.Product;

        Assert.Equal("CVD Inbox", product.Name);
        Assert.Equal("1.2.0", product.Version);
        Assert.Equal("pkg:github/CosmaVito/cra-trail", product.Purl);
        Assert.Equal("2031-12-31", product.SupportEnd);
        Assert.Equal("security@example.org", product.CvdContact);
        Assert.Equal("attested/docs/risk-assessment.md", product.Attested["annex-i/part-i/1"]);
        Assert.Equal(new[] { "docs/risk-assessment.md" }, config.AttestedSources);
        Assert.Equal("No network functions that affect other devices.", product.NotApplicable["annex-i/part-i/2i"]);
        Assert.Equal("Receive vulnerability reports.", product.UserInfo["intended-purpose"]);
        Assert.Equal(7.5, product.CheckOverrides["no-critical-without-vex"]["select"]!["min-cvss"]!.GetValue<double>());
        Assert.Equal(new[] { "no-kev-without-vex" }, product.GateExclude);
        Assert.Equal(new[] { "sbom-bsi-fields" }, product.GateInclude);
    }

    [Fact]
    public void Only_the_product_name_is_required()
    {
        var product = CraConfigLoader.Load("product:\n  name: X\n", "1.0.0", Rules, _ => false).Product;

        Assert.Null(product.SupportEnd);
        Assert.Null(product.CvdContact);
        Assert.Empty(product.Attested);
        Assert.Empty(product.CheckOverrides);
        Assert.Empty(product.GateInclude);
        Assert.Empty(product.GateExclude);
    }

    [Theory]
    [InlineData("product:\n  name: X\nextra: 1\n", "cra.yml: unknown key 'extra'.")]
    [InlineData("product:\n  name: X\n  owner: me\n", "product: unknown key 'owner'.")]
    [InlineData("product:\n  purl: pkg:x/y\n", "product.name is required.")]
    [InlineData("product:\n  name: X\n  support-end: 31/12/2031\n", "product.support-end must be a date in the form YYYY-MM-DD.")]
    [InlineData("product:\n  name: X\n  security-txt: http://example.org/security.txt\n", "product.security-txt must be an https URL.")]
    [InlineData("product:\n  name: X\nattested:\n  annex-i/part-i/9: a.md\n", "attested: unknown requirement 'annex-i/part-i/9'.")]
    [InlineData("product:\n  name: X\nattested:\n  annex-i/part-i/1: ../outside.md\n", "attested: '../outside.md' must be a relative path inside the repository.")]
    [InlineData("product:\n  name: X\nattested:\n  annex-i/part-i/1: missing.md\n", "attested: document not found: missing.md")]
    [InlineData("product:\n  name: X\nnot-applicable:\n  annex-i/part-i/2i: ''\n", "not-applicable: the reason for 'annex-i/part-i/2i' is empty.")]
    [InlineData("product:\n  name: X\ngate:\n  max-cvss-without-vex: 11\n", "gate.max-cvss-without-vex must be a number from 0 to 10.")]
    [InlineData("product:\n  name: X\ngate:\n  require-bsi: yes\n", "gate.require-bsi must be true or false.")]
    [InlineData("- just\n- a list\n", "cra.yml must be a mapping.")]
    public void Reports_configuration_errors(string yaml, string expected) =>
        Assert.Contains(expected, Errors(yaml, "1.0.0"));

    [Fact]
    public void A_point_cannot_be_both_attested_and_not_applicable() =>
        Assert.Contains(
            "'annex-i/part-i/1' cannot be both attested and not-applicable.",
            Assert.Throws<ConfigException>(() => CraConfigLoader.Load(
                "product:\n  name: X\nattested:\n  annex-i/part-i/1: a.md\nnot-applicable:\n  annex-i/part-i/1: Reason.\n",
                "1.0.0", Rules, _ => true)).Errors);

    [Fact]
    public void The_version_is_required() =>
        Assert.Contains("The product version is required.", Errors("product:\n  name: X\n", " "));

    [Fact]
    public void Gate_options_need_their_checks_in_the_rules()
    {
        var bare = RuleSetLoader.Load("version: \"1\"\ngate: []\nchecks: {}\nrequirements: []\n", []);
        var errors = Assert.Throws<ConfigException>(() => CraConfigLoader.Load(
            "product:\n  name: X\ngate:\n  require-bsi: true\n", "1.0.0", bare, _ => false)).Errors;
        Assert.Contains("gate.require-bsi needs the check 'sbom-bsi-fields' in the rules.", errors);
    }

    private static IReadOnlyList<string> Errors(string yaml, string version) =>
        Assert.Throws<ConfigException>(() => CraConfigLoader.Load(yaml, version, Rules, _ => false)).Errors;
}
