using System.Text.Json;
using CraTrail.Core.Json;

namespace CraTrail.Core.Pack;

public sealed record VerificationResult(bool Ok, string? ManifestSha256, PackManifest? Manifest, IReadOnlyList<string> Problems);

public static class PackVerifier
{
    public static VerificationResult Verify(IReadOnlyDictionary<string, byte[]> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (!entries.TryGetValue(PackManifest.FileName, out var manifestBytes))
            return new VerificationResult(false, null, null, ["manifest.json is missing"]);

        var manifestSha = PackHashing.Sha256Hex(manifestBytes);
        PackManifest manifest;
        try
        {
            manifest = CraTrailJson.Deserialize<PackManifest>(manifestBytes);
        }
        catch (JsonException ex)
        {
            return new VerificationResult(false, manifestSha, null, [$"manifest.json is not valid: {ex.Message}"]);
        }
        if (manifest.Files.Contains(null) || manifest.Tools.Contains(null))
        {
            var list = manifest.Files.Contains(null) ? "files" : "tools";
            return new VerificationResult(false, manifestSha, null, [$"manifest.json is not valid: null entry in {list}"]);
        }

        var problems = new List<string>();
        if (manifest.SchemaVersion != PackManifest.CurrentSchemaVersion)
            problems.Add($"unsupported schema version '{manifest.SchemaVersion}'");

        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (!IsSafePath(file.Path))
            {
                problems.Add($"unsafe path: {file.Path}");
                continue;
            }
            if (!listed.Add(file.Path))
            {
                problems.Add($"duplicate path: {file.Path}");
                continue;
            }
            if (!entries.TryGetValue(file.Path, out var bytes))
            {
                problems.Add($"missing file: {file.Path}");
                continue;
            }
            if (bytes.LongLength != file.Size || PackHashing.Sha256Hex(bytes) != file.Sha256)
                problems.Add($"hash mismatch: {file.Path}");
        }
        foreach (var path in entries.Keys.Where(p => p != PackManifest.FileName && !listed.Contains(p)))
            problems.Add($"unexpected file: {path}");

        problems.Sort(StringComparer.Ordinal);
        return new VerificationResult(problems.Count == 0, manifestSha, manifest, problems);
    }

    public static bool IsSafePath(string path) =>
        !string.IsNullOrEmpty(path)
        && !path.Contains('\\', StringComparison.Ordinal)
        && !path.Contains(':', StringComparison.Ordinal)
        && !path.StartsWith('/')
        && path.Split('/').All(segment => segment is not ("" or "." or ".."));
}
