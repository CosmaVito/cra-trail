using System.Text.Json.Nodes;
using CraTrail.Core.Json;
using CraTrail.Core.Pack;

namespace CraTrail.Tests.Pack;

public class PackVerifierTests
{
    [Fact]
    public void A_built_pack_verifies()
    {
        var pack = Pack();
        var result = PackVerifier.Verify(Entries(pack));

        Assert.True(result.Ok);
        Assert.Empty(result.Problems);
        Assert.Equal(PackHashing.Sha256Hex(pack.ManifestBytes), result.ManifestSha256);
        Assert.Equal(pack.ManifestBytes, PackContents.FromVerified(result, Entries(pack)).ManifestBytes);
    }

    [Fact]
    public void A_changed_byte_is_a_hash_mismatch()
    {
        var entries = Entries(Pack());
        entries["sbom/product.cdx.json"] = "{ }"u8.ToArray();
        Assert.Equal(new[] { "hash mismatch: sbom/product.cdx.json" }, PackVerifier.Verify(entries).Problems);
    }

    [Fact]
    public void Missing_and_unexpected_files_are_reported()
    {
        var entries = Entries(Pack());
        entries.Remove("sbom/product.cdx.json");
        entries["extra.txt"] = [1];
        Assert.Equal(new[] { "missing file: sbom/product.cdx.json", "unexpected file: extra.txt" }, PackVerifier.Verify(entries).Problems);
    }

    [Fact]
    public void Unsafe_and_duplicate_manifest_paths_are_reported()
    {
        var pack = Pack();
        var file = pack.Manifest.Files[0];
        var manifest = pack.Manifest with { Files = [.. pack.Manifest.Files, file, file with { Path = "../evil" }] };
        var entries = Entries(pack);
        entries[PackManifest.FileName] = CraTrailJson.Serialize(manifest);

        var problems = PackVerifier.Verify(entries).Problems;
        Assert.Contains("unsafe path: ../evil", problems);
        Assert.Contains($"duplicate path: {file.Path}", problems);
    }

    [Fact]
    public void A_missing_or_broken_manifest_fails()
    {
        var entries = Entries(Pack());
        entries[PackManifest.FileName] = "{"u8.ToArray();
        Assert.StartsWith("manifest.json is not valid", Assert.Single(PackVerifier.Verify(entries).Problems), StringComparison.Ordinal);

        entries.Remove(PackManifest.FileName);
        Assert.Equal(new[] { "manifest.json is missing" }, PackVerifier.Verify(entries).Problems);
    }

    [Fact]
    public void Null_manifest_entries_fail_verification()
    {
        var entries = Entries(Pack());
        var manifest = JsonNode.Parse(entries[PackManifest.FileName])!;
        manifest["files"]!.AsArray().Add(null);
        entries[PackManifest.FileName] = CraTrailJson.SerializeNode(manifest);

        var result = PackVerifier.Verify(entries);
        Assert.False(result.Ok);
        Assert.Equal(new[] { "manifest.json is not valid: null entry in files" }, result.Problems);
    }

    // Shared with SchemaTests: the manifest schema must accept exactly the paths the verifier accepts.
    public static TheoryData<string, bool> Paths { get; } = new()
    {
        { "sbom/product.cdx.json", true },
        { "product.json", true },
        { "attested/docs/.well-known.md", true },
        { "a/..b", true },
        { "../evil", false },
        { "a/../b", false },
        { "a/..", false },
        { "./a", false },
        { "/etc/passwd", false },
        { "a\\b", false },
        { "C:/x", false },
        { "scans/codeql:cs.sarif", false },
        { "a//b", false },
        { "a/./b", false },
        { "a/", false },
        { "", false },
    };

    [Theory]
    [MemberData(nameof(Paths))]
    public void Safe_paths_are_relative_and_plain(string path, bool safe) => Assert.Equal(safe, PackVerifier.IsSafePath(path));

    private static PackContents Pack() =>
        new TestPackBuilder().Add("sbom/product.cdx.json", EvidenceKinds.ProductSbom, "{}").Build();

    private static Dictionary<string, byte[]> Entries(PackContents pack) =>
        new(pack.Files, StringComparer.Ordinal) { [PackManifest.FileName] = pack.ManifestBytes };
}
