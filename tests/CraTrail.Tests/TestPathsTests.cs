namespace CraTrail.Tests;

public class TestPathsTests
{
    [Fact]
    public void Repo_root_contains_the_solution() =>
        Assert.True(File.Exists(Path.Combine(TestPaths.RepoRoot, "CraTrail.slnx")));
}
