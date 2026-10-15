using System.IO.Compression;
using CraTrail.Core.Pack;

namespace CraTrail.Cli;

internal static class PackArchive
{
    public const long DefaultMaxBytes = 200L * 1024 * 1024;

    private static readonly DateTimeOffset EntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Write(PackContents pack, Stream output)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(zip, PackManifest.FileName, pack.ManifestBytes);
        foreach (var (path, bytes) in pack.Files.OrderBy(f => f.Key, StringComparer.Ordinal))
            WriteEntry(zip, path, bytes);
    }

    public static IReadOnlyDictionary<string, byte[]> Read(Stream input, long maxBytes = DefaultMaxBytes)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new PackFormatException($"Not a zip file: {ex.Message}");
        }

        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        using (zip)
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/'))
                    continue;
                if (!PackVerifier.IsSafePath(entry.FullName))
                    throw new PackFormatException($"Unsafe path in pack: {entry.FullName}");
                if (entries.ContainsKey(entry.FullName))
                    throw new PackFormatException($"Duplicate entry in pack: {entry.FullName}");

                using var source = entry.Open();
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
                {
                    total += read;
                    if (total > maxBytes)
                        throw new PackFormatException($"The pack is larger than {maxBytes} bytes once extracted.");
                    buffer.Write(chunk, 0, read);
                }
                entries[entry.FullName] = buffer.ToArray();
            }
        }
        return entries;
    }

    private static void WriteEntry(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = EntryTime;
        using var stream = entry.Open();
        stream.Write(bytes);
    }
}
