using System.IO.Compression;
using CraTrail.Cli;
using CraTrail.Core.Pack;

namespace CraTrail.Tests.Cli;

public class PackArchiveTests
{
    [Fact]
    public void Round_trip_keeps_every_entry_with_the_manifest_first()
    {
        var pack = Pack();
        var bytes = Write(pack);

        var entries = PackArchive.Read(new MemoryStream(bytes));
        Assert.Equal(pack.ManifestBytes, entries[PackManifest.FileName]);
        Assert.All(pack.Files, f => Assert.Equal(f.Value, entries[f.Key]));
        Assert.Equal(pack.Files.Count + 1, entries.Count);

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal(PackManifest.FileName, zip.Entries[0].FullName);
    }

    [Fact]
    public void Writing_twice_gives_the_same_bytes() => Assert.Equal(Write(Pack()), Write(Pack()));

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("a\\b.txt")]
    [InlineData("C:/x.txt")]
    public void Unsafe_entry_names_are_rejected(string name)
    {
        var error = Assert.Throws<PackFormatException>(() => PackArchive.Read(Zip((name, "x"))));
        Assert.Equal($"Unsafe path in pack: {name}", error.Message);
    }

    [Fact]
    public void Duplicate_entries_are_rejected() =>
        Assert.Equal(
            "Duplicate entry in pack: a.txt",
            Assert.Throws<PackFormatException>(() => PackArchive.Read(Zip(("a.txt", "1"), ("a.txt", "2")))).Message);

    [Fact]
    public void Oversized_content_is_rejected() =>
        Assert.Throws<PackFormatException>(() => PackArchive.Read(Zip(("a.txt", new string('x', 2048))), maxBytes: 1024));

    [Fact]
    public void Other_files_are_rejected() =>
        Assert.StartsWith(
            "Not a zip file",
            Assert.Throws<PackFormatException>(() => PackArchive.Read(new MemoryStream("not a zip"u8.ToArray()))).Message,
            StringComparison.Ordinal);

    [Fact]
    public void Folder_entries_are_skipped() =>
        Assert.Equal(new[] { "a/b.txt" }, PackArchive.Read(Zip(("a/", ""), ("a/b.txt", "x"))).Keys);

    [Fact]
    public void A_damaged_entry_is_rejected()
    {
        var bytes = Write(Pack());
        var nameLength = BitConverter.ToUInt16(bytes, 26);
        var extraLength = BitConverter.ToUInt16(bytes, 28);
        bytes[30 + nameLength + extraLength] = 0xFF;

        var error = Assert.Throws<PackFormatException>(() => PackArchive.Read(new MemoryStream(bytes)));
        Assert.Equal("Damaged entry in pack: manifest.json", error.Message);
    }

    private static PackContents Pack() =>
        new TestPackBuilder().Add("sbom/product.cdx.json", EvidenceKinds.ProductSbom, "{}").Add("scans/grype.json", EvidenceKinds.Grype, "{}").Build();

    private static byte[] Write(PackContents pack)
    {
        using var stream = new MemoryStream();
        PackArchive.Write(pack, stream);
        return stream.ToArray();
    }

    private static MemoryStream Zip(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }
        }
        stream.Position = 0;
        return stream;
    }
}
