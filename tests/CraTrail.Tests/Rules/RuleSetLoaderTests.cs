using System.Text.Json.Nodes;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Rules;

public class RuleSetLoaderTests
{
    private const string Profile = """
        id: core
        title: Core fields
        fields:
          - id: timestamp
            path: metadata.timestamp
            require: non-empty
          - id: hash
            each: components
            path: hashes[*].alg
            require:
              contains: SHA-512
          - id: kind
            each: all-components
            skip-assemblies: true
            paths: ["properties[name=kind].value"]
            ref: "5.2.2"
            require:
              one-of: [a, b]
        """;

    private const string Crosswalk = """
        version: "2026.10"
        gate: [sbom-present]
        checks:
          sbom-present:
            type: file-present
            kind: product-sbom
            hint: Add the SBOM.
          sbom-fields:
            type: json-field-required
            kind: product-sbom
            profile: core
          no-kev:
            type: vex-covers
            kind: grype
            select:
              kev: true
          no-secrets:
            type: scan-threshold
            kind: secrets-sarif
            levels: [error, warning]
          support-end:
            type: attested-field
            field: product.supportEnd
        requirements:
          - id: annex-i/part-ii/1
            point: II (1)
            title: Components identified
            source: Annex I, Part II, point 1
            coverage: automated
            checks: [sbom-present, sbom-fields]
            refs:
              osps: [OSPS-QA-02.01]
          - id: annex-i/part-i/1
            point: I (1)
            title: Risk based cybersecurity
            source: Annex I, Part I, point 1
            coverage: manual
            checks: []
        """;

    private const string Minimal = "version: \"1\"\ngate: []\nchecks: {}\nrequirements: []\n";

    [Fact]
    public void Loads_checks_requirements_gate_and_profiles()
    {
        var rules = RuleSetLoader.Load(Crosswalk, [Profile]);

        Assert.Equal("2026.10", rules.Version);
        Assert.Equal(new[] { "sbom-present" }, rules.Gate);
        Assert.Equal(
            new[] { "sbom-present", "sbom-fields", "no-kev", "no-secrets", "support-end" },
            rules.Checks.Select(c => c.Id));

        var noKev = rules.FindCheck("no-kev")!;
        Assert.Equal(CheckTypes.VexCovers, noKev.Type);
        Assert.True(noKev.GetObject("select")!["kev"]!.GetValue<bool>());
        Assert.Null(noKev.Parameters["type"]);
        Assert.Equal("Add the SBOM.", rules.FindCheck("sbom-present")!.Hint);
        Assert.Equal(new[] { "error", "warning" }, rules.FindCheck("no-secrets")!.GetStrings("levels"));

        var requirement = rules.FindRequirement("annex-i/part-ii/1")!;
        Assert.Equal("II (1)", requirement.Point);
        Assert.Equal(Coverage.Automated, requirement.Coverage);
        Assert.Equal(new[] { "OSPS-QA-02.01" }, requirement.Refs["osps"]);
        Assert.Empty(rules.FindRequirement("annex-i/part-i/1")!.Checks);

        var fields = rules.Profiles["core"].Fields;
        Assert.Equal(3, fields.Count);
        Assert.Equal(FieldRequirementKinds.Contains, fields[1].Require.Kind);
        Assert.Equal(new[] { "SHA-512" }, fields[1].Require.Values);
        Assert.Equal(EachScopes.AllComponents, fields[2].Each);
        Assert.True(fields[2].SkipAssemblies);
        Assert.Equal("5.2.2", fields[2].Reference);
        Assert.Equal(new[] { "a", "b" }, fields[2].Require.Values);
        Assert.Null(rules.FindCheck("ghost"));
    }

    [Fact]
    public void Overrides_replace_parameters_without_touching_the_original()
    {
        var check = RuleSetLoader.Load(Crosswalk, [Profile]).FindCheck("no-kev")!;
        var changed = check.WithOverrides(new JsonObject { ["select"] = new JsonObject { ["min-cvss"] = 7.0 } });

        Assert.Equal(7.0, changed.GetObject("select")!["min-cvss"]!.GetValue<double>());
        Assert.True(check.GetObject("select")!["kev"]!.GetValue<bool>());
        Assert.Same(check, check.WithOverrides(null));
    }

    [Theory]
    [InlineData("type: nope", "check 'x': unknown type 'nope'.")]
    [InlineData("type: file-present", "check 'x': 'kind' must be a non-empty string.")]
    [InlineData("type: json-field-required\n    kind: product-sbom\n    profile: missing", "check 'x': unknown profile 'missing'.")]
    [InlineData("type: attested-field\n    field: supportEnd", "check 'x': 'field' must be a path starting with 'product.' or 'manifest.'.")]
    [InlineData("type: scan-threshold\n    kind: sast-sarif\n    levels: [fatal]", "check 'x': unknown SARIF level 'fatal'.")]
    [InlineData("type: scan-threshold\n    kind: sast-sarif\n    max-results: -1", "check 'x': 'max-results' must be zero or more.")]
    [InlineData("type: vex-covers\n    kind: sast-sarif", "check 'x': 'kind' must be grype.")]
    public void Invalid_checks_are_reported(string body, string expected)
    {
        var crosswalk = $"version: \"1\"\ngate: []\nchecks:\n  x:\n    {body}\nrequirements: []\n";
        Assert.Equal(new[] { expected }, Errors(crosswalk));
    }

    [Fact]
    public void Requirement_errors_are_collected_together()
    {
        var errors = Errors("""
            version: "1"
            gate: [ghost]
            checks:
              present:
                type: file-present
                kind: product-sbom
            requirements:
              - id: Bad Id
                point: X
                title: T
                source: S
                coverage: manual
              - id: a/b
                point: X
                title: T
                source: S
                coverage: manual
                checks: [present]
              - id: a/c
                point: X
                title: T
                source: S
                coverage: automated
              - id: a/c
                point: X
                title: ""
                source: S
                coverage: sometimes
                checks: [unknown]
            """);

        Assert.Equal(new[]
        {
            "requirement 'Bad Id': id must use lowercase letters, digits, '-' and '/'.",
            "requirement 'a/b': manual coverage cannot list checks.",
            "requirement 'a/c': automated coverage needs at least one check.",
            "requirement 'a/c': duplicate id.",
            "requirement 'a/c': 'title' must be a non-empty string.",
            "requirement 'a/c': unknown check 'unknown'.",
            "requirement 'a/c': coverage must be automated, partial or manual.",
            "gate: unknown check 'ghost'.",
        }, errors);
    }

    [Fact]
    public void Profile_errors_are_reported()
    {
        var errors = Errors(Minimal, """
            id: p
            title: P
            fields:
              - id: f
                each: everything
                path: a..b
                require: maybe
            """);

        Assert.Equal(new[]
        {
            "profile 'p' field 'f': 'each' must be components or all-components.",
            "profile 'p' field 'f': invalid path 'a..b'.",
            "profile 'p' field 'f': 'require' must be present, non-empty, absent, { contains: X } or { one-of: [...] }.",
        }, errors);
    }

    [Fact]
    public void Broken_yaml_and_missing_version_are_reported()
    {
        Assert.Equal(new[] { "crosswalk: 'version' must be a non-empty string." }, Errors("gate: []\nchecks: {}\nrequirements: []\n"));
        Assert.StartsWith("crosswalk: Invalid YAML", Errors("version: [")[0], StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> Errors(string crosswalk, params string[] profiles) =>
        Assert.Throws<RuleSetException>(() => RuleSetLoader.Load(crosswalk, profiles)).Errors;
}
