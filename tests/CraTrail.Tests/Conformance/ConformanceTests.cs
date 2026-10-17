using System.Text.Json.Nodes;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Json;

namespace CraTrail.Tests.Conformance;

public class ConformanceTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(TestPaths.Conformance("cases")).Order(StringComparer.Ordinal))
            data.Add(Path.GetFileName(directory));
        return data;
    }

    [Fact]
    public void There_are_fourteen_cases() => Assert.Equal(14, Directory.GetDirectories(TestPaths.Conformance("cases")).Length);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Evaluation_matches_expected(string name)
    {
        var rules = TestRules.Conformance;
        var pack = ConformanceCase.LoadPack(TestPaths.Conformance("cases", name), rules.Version);
        var actual = JsonNode.Parse(CraTrailJson.Serialize(Evaluator.CreateDefault().Evaluate(pack, rules)));
        var expected = JsonNode.Parse(File.ReadAllBytes(TestPaths.Conformance("cases", name, "expected.json")));

        if (!JsonNode.DeepEquals(expected, actual))
        {
            var output = Path.Combine(TestPaths.RepoRoot, "artifacts", "conformance", name + ".actual.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllBytes(output, CraTrailJson.SerializeNode(actual!));
            Assert.Fail($"The evaluation differs from expected.json. The actual result is in {output}");
        }
    }
}
