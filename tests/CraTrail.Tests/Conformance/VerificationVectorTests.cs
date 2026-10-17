using System.Text.Json.Nodes;
using CraTrail.Core.Pack;

namespace CraTrail.Tests.Conformance;

public class VerificationVectorTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(TestPaths.Conformance("verification")).Order(StringComparer.Ordinal))
            data.Add(Path.GetFileName(directory));
        return data;
    }

    [Fact]
    public void There_are_ten_cases() => Assert.Equal(10, Directory.GetDirectories(TestPaths.Conformance("verification")).Length);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Verification_matches_expected(string name)
    {
        var folder = TestPaths.Conformance("verification", name, "pack");
        var entries = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes, StringComparer.Ordinal);
        var expected = JsonNode.Parse(File.ReadAllBytes(TestPaths.Conformance("verification", name, "expected.json")))!;

        var result = PackVerifier.Verify(entries);

        Assert.Equal(expected["ok"]!.GetValue<bool>(), result.Ok);
        Assert.Equal(expected["manifestSha256"]?.GetValue<string>(), result.ManifestSha256);
        Assert.Equal(expected["problems"]!.AsArray().Select(problem => problem!.GetValue<string>()), result.Problems);
    }

    [Fact]
    public void An_edited_manifest_keeps_matching_hashes_but_gets_another_digest()
    {
        var clean = JsonNode.Parse(File.ReadAllBytes(TestPaths.Conformance("verification", "clean", "expected.json")))!;
        var edited = JsonNode.Parse(File.ReadAllBytes(TestPaths.Conformance("verification", "manifest-edited", "expected.json")))!;

        Assert.True(edited["ok"]!.GetValue<bool>());
        Assert.NotEqual(clean["manifestSha256"]!.GetValue<string>(), edited["manifestSha256"]!.GetValue<string>());
    }
}
