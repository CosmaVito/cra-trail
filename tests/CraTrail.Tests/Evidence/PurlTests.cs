using CraTrail.Core.Evidence;

namespace CraTrail.Tests.Evidence;

public class PurlTests
{
    [Theory]
    [InlineData("pkg:nuget/Newtonsoft.Json@12.0.1", "pkg:nuget/newtonsoft.json@12.0.1", true)]
    [InlineData("pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1?type=jar", "pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1", true)]
    [InlineData("pkg:npm/%40angular/core@22.2.2", "pkg:npm/@angular/core@22.2.2", true)]
    [InlineData("pkg:npm/minimist", "pkg:npm/minimist@1.2.0", true)]
    [InlineData("pkg:npm/minimist@1.2.6", "pkg:npm/minimist@1.2.0", false)]
    [InlineData("pkg:npm/minimist@1.2.0", "pkg:nuget/minimist@1.2.0", false)]
    [InlineData("pkg:npm/minimist@1.2.0", null, false)]
    [InlineData("https://example.org/product", "pkg:npm/minimist@1.2.0", false)]
    public void Matches_compares_type_namespace_name_and_version(string statement, string? finding, bool expected) =>
        Assert.Equal(expected, Purl.Matches(statement, finding));

    [Fact]
    public void Parses_namespace_and_version()
    {
        Assert.True(Purl.TryParse("pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1#sub", out var parts));
        Assert.Equal(new PurlParts("maven", "org.apache.logging.log4j", "log4j-core", "2.14.1"), parts);
    }
}
