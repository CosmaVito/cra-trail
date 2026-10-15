using System.Text.Json.Nodes;
using CraTrail.Core.Evidence;

namespace CraTrail.Tests.Evidence;

public class GrypeTests
{
    private static readonly GrypeReport Report =
        Grype.Parse(JsonNode.Parse(File.ReadAllBytes(TestPaths.Fixture("grype", "sample.json")))!);

    [Fact]
    public void Reads_descriptor_matches_and_ignored_matches()
    {
        Assert.Equal("0.120.1", Report.GrypeVersion);
        Assert.Equal("2026-10-14T06:30:34Z", Report.DatabaseBuilt);
        Assert.Equal(3, Report.Matches.Count);
        Assert.Equal("GHSA-xvch-5gv4-984h", Assert.Single(Report.Ignored).Id);
    }

    [Fact]
    public void Collects_related_ids_scores_and_fixes()
    {
        var finding = Report.Matches.Single(f => f.Id == "GHSA-5crp-9r3c-p9vr");

        Assert.Equal(new[] { "GHSA-5crp-9r3c-p9vr", "CVE-2024-21907" }, finding.Ids);
        Assert.Equal("Newtonsoft.Json", finding.Name);
        Assert.Equal("12.0.1", finding.Version);
        Assert.Equal("pkg:nuget/Newtonsoft.Json@12.0.1", finding.Purl);
        Assert.Equal("ref-newtonsoft", finding.BomRef);
        Assert.Equal(7.5, finding.MaxCvss);
        Assert.Equal(0.32908, finding.Epss);
        Assert.False(finding.Kev);
        Assert.Equal(new[] { "13.0.1" }, finding.FixVersions);
    }

    [Fact]
    public void Kev_comes_from_the_vulnerability_or_a_related_record()
    {
        var finding = Report.Matches.Single(f => f.Id == "GHSA-jfh8-c2jp-5v3q");
        Assert.True(finding.Kev);
        Assert.Equal(10.0, finding.EffectiveScore);
        Assert.Equal(0.99999, finding.Epss);
    }

    [Fact]
    public void Severity_stands_in_for_a_missing_cvss_score()
    {
        var finding = Report.Matches.Single(f => f.Id == "GHSA-aaaa-bbbb-cccc");
        Assert.Null(finding.MaxCvss);
        Assert.Equal(9.0, finding.EffectiveScore);
        Assert.Null(finding.Epss);
    }

    [Theory]
    [InlineData("Critical", 9.0)]
    [InlineData("high", 7.0)]
    [InlineData("Medium", 4.0)]
    [InlineData("Low", 0.1)]
    [InlineData("Negligible", 0.0)]
    [InlineData("Unknown", 0.0)]
    public void Severity_scores(string severity, double score) => Assert.Equal(score, Grype.SeverityScore(severity));

    [Fact]
    public void Other_json_is_rejected() =>
        Assert.Throws<FormatException>(() => Grype.Parse(JsonNode.Parse("""{ "matches": [] }""")!));
}
