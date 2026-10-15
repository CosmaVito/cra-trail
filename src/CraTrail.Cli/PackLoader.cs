using CraTrail.Core.Pack;

namespace CraTrail.Cli;

internal static class PackLoader
{
    public static (PackContents? Pack, VerificationResult Result) Load(CliIO io, string path)
    {
        IReadOnlyDictionary<string, byte[]> entries;
        using (var stream = File.OpenRead(io.Resolve(path)))
            entries = PackArchive.Read(stream);

        var result = PackVerifier.Verify(entries);
        if (!result.Ok)
        {
            foreach (var problem in result.Problems)
                io.Err.WriteLine($"problem: {problem}");
            return (null, result);
        }
        return (PackContents.FromVerified(result, entries), result);
    }
}
