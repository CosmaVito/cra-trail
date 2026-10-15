using System.Text;
using CvdInbox.Data;

namespace CvdInbox.Tests;

// Reads the database files as raw bytes, the way someone with a copy of the data volume would.
internal static class DataFiles
{
    public static bool Contain(string dataDirectory, string marker)
    {
        var needle = Encoding.UTF8.GetBytes(marker);
        foreach (var path in Directory.EnumerateFiles(dataDirectory, DataSetup.DatabaseFile + "*"))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            if (copy.ToArray().AsSpan().IndexOf(needle) >= 0)
                return true;
        }
        return false;
    }
}
